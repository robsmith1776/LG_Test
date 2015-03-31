using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Collections;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;
using System.Timers;
using System.Data;

using FreshAddress.Logging;
using FreshAddress.Databases;

namespace FreshAddress.REACT
{
    //REACT ENGINE
    public class REACTer
    {
        public enum ATC { n, c, a } //none, conservative (default), aggressive (if ecoa)

        private string URL;
        private string batchID = "0";   //tied to Auditor IDs
        private int microBatchID = 0;   //for counting which batch of email addresses being processed
        private int currentRetryQueue = 0;
        private ATC atcMode = ATC.c;
        private string buildInfo = AppData.BuildTime().ToString("yyyyMMdd");

        private DateTime lastAuditorCheck = DateTime.Now.AddDays(-1);   //to scale threads and occasionally check
        private int runningThreads = 1;

        private DateTime lastRTsend = DateTime.Now; //last hit to RT URL (for pacing requests)
#if DEBUG
        private int maxReqPerSecond = 200;   //test at X records per second
#else
        private int maxReqPerSecond = 200;  //for production? to get number from sunny
#endif

        private string baseURL = "http://rt-local.freshaddress.biz:8080";

        //for logging issues, give outside access for flushing, setting dir, etc
        public LogWriter perfLog = new LogWriter(Directory.GetCurrentDirectory(), "REACT");

        private System.Timers.Timer logTimer;
        private bool perfEnabled = false;

        //CONSTRUCTORS
        public REACTer()
        {
            buildURL();
        }
        public REACTer(string batch)
        {
            batchID = batch;
            buildURL();
        }

        //for logging
        public void setPerformanceLogging(bool log)
        {
            perfEnabled = log;
        }

        /// <summary>
        /// calculates the latency to insert b/w each call to rt-local
        /// </summary>
        /// <returns></returns>
        private int calculateSleepMS()
        {
            double reqPerMS = 1000 / maxReqPerSecond;
            int sleepFrequency = (int) Math.Ceiling(reqPerMS);
            if (sleepFrequency < 0)
            {
                sleepFrequency = 0;
            }

//skip this in debug mode if want to test target rate code
#if !DEBUG
            //occasionally (not every call!) check Auditor for # of running threads in REACT stage
            if ((DateTime.Now - lastAuditorCheck).TotalMinutes >= 5)
            {
                lastAuditorCheck = DateTime.Now;
                DB ITS = FreshAddressDatabase.getDB("", "");
                DataTable running = ITS.dataQuery("select count(*) as Total from auditor_log where outcomecomment like '%through REACT%' and outcome is null");
                try
                {
                    runningThreads = Convert.ToInt32(running.Rows[0]["Total"].ToString());
                }
                catch { }
                ITS.disconnect();
            }

            //don't multiply (next step) by 0, there is at least 1 running!
            if (runningThreads == 0)
            {
                runningThreads = 1;
            }

            //multiply out by # so as to make sure each thread averages out to target runtime
            sleepFrequency = sleepFrequency * runningThreads;
#endif

            return sleepFrequency;
        }
        public int batchSize()
        {
            //per sunny's request, going to set batch size as a constant variable
            return 200; //setting same as ideal req per second
        }

        //methods to update URL
        private void buildURL()
        {
            //base (do not consult deliv as done in code and do not use RTC)
            URL = this.baseURL + "/react7?dnscache=true&rtc=false&deliv=false&mx=true&advanced=true&atc=" + atcMode.ToString().ToLower();
            URL += "&batch_id=" + batchID;
            URL += "&mbid=" + microBatchID.ToString();
            URL += "&retry=" + currentRetryQueue.ToString();
            URL += "&build=" + buildInfo;
        }
        private void randomizeURL()
        {
            //reset to base
            buildURL();

            //generate random URL
            Random rand = new Random();
            int rtNumber = rand.Next(1, 64);
            string rtRand = rtNumber.ToString("D2"); //format as 01-64

            this.URL = this.URL.Replace("rt-local", "rt-local-" + rtRand);
        }
        public void setRetryQueue(int count)
        {
            currentRetryQueue = count;
            buildURL();
        }
        public void setATC(ATC modeForAtc)
        {
            atcMode = modeForAtc;
            buildURL();
        }

        
        //async method to parse URL
        public async Task<REACToutput> reactEmail(string e)
        {
            //randomize before calling
            randomizeURL();

            string emailNoEncode = e;   //for logging

            if (e.Trim() == "")
            {
                e = "%20%09";
            }
            else
            {
                e = HttpUtility.UrlEncode(e);
            }
            string URLwEmail = URL + "&email=" + e;


            // Create the HttpClientHandler which will handle cookies.
            var handler = new HttpClientHandler();

            // Set cookies on handler.

            // Await on an async call to fetch here, convert to a data
            // set and return.
            var client = new HttpClient(handler);

            client.Timeout = TimeSpan.FromMilliseconds(1000 * 20);

            //Console.WriteLine(url);

            //capture runtime
            DateTime rStart = DateTime.Now;
            DateTime rEnd = DateTime.Now;

            //result
            REACToutput rOut = null;

            // Wait for the HttpResponseMessage.
            string content = null;
            try
            {
                //add wait if needed
                double msSinceLastRT = (DateTime.Now - lastRTsend).TotalMilliseconds;
                int addedWait = 0;
                int threadSleep = calculateSleepMS();
                if (msSinceLastRT < threadSleep)
                {
                    addedWait = threadSleep - (int)msSinceLastRT;
                    Thread.Sleep(addedWait);
                    //DEBUG: Console.WriteLine("Added: " + addedWait + " | sleep calc: " + threadSleep);
                }
                lastRTsend = DateTime.Now;

                rStart = DateTime.Now;
                HttpResponseMessage response = await client.GetAsync(new Uri(URLwEmail));

                // Get the content, await on the string content.
                content = await response.Content.ReadAsStringAsync();

                //capture react end time
                rEnd = DateTime.Now;

                //parse response
                rOut = JsonConvert.DeserializeObject<REACToutput>(content);
                rOut.REACTend = rEnd;
                rOut.REACTstart = rStart;
                rOut.runtime = (rEnd - rStart).TotalMilliseconds;

                if (perfEnabled)
                {
                    //due to batching/async, runtime and end time not being captured correctly, to eventually figure out
                    //ApplicationLog.Log(rOut.fai_servertime + "\t" + rOut.runtime + "\t" + rStart.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + rEnd.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + emailNoEncode + "\t" + URLwEmail);

                    //log sunny server time, added wait for request, # of running threads, start time   
                    perfLog.Log(rOut.fai_servertime + "\t" + addedWait + "\t" + runningThreads + "\t" + rStart.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + emailNoEncode + "\t" + URLwEmail);
                }

            }
            catch { 
                //log error
                perfLog.Log("Error getting/parsing JSON response from: " + URLwEmail);
                rEnd = DateTime.Now;
            }

            //sleep here if need to pace requests
            //Thread.Sleep(calculateSleepMS());
            //await Task.Delay(calculateSleepMS());

            // Process content variable here into a data set and return.
            return rOut;
        }

        //async method to REACT a collection of emails and return a collection of REACToutput objects
        public async Task<ArrayList> reactBatch(ArrayList emails)
        {
            this.microBatchID++;
           
            var tasks = new List<Task<REACToutput>>();

            foreach (Record r in emails)
            {
                tasks.Add(reactEmail(r.email));
            }

            await Task.WhenAll(tasks);

            int i = 0;
            foreach (Task<REACToutput> rOut in tasks)
            {
                ((Record)emails[i]).result = rOut.Result;
                i++;                
            }

            return emails;
        }
        
    }

    //REACT input record
    public class Record
    {
        public string id = "";
        public string email = "";
        public string brand = "";
        public string domain = "";
        public string baseDomain = "";
        public REACToutput result;

        ArrayList headerFields = new ArrayList();
        ArrayList outputFields = new ArrayList();

        /// <summary>
        /// creates a record
        /// </summary>
        /// <param name="i">id</param>
        /// <param name="e">email</param>
        public Record(string i, string e)
        {
            id = i;
            email = e;
            init();
        }
        public Record(string i, string e, string b)
        {
            id = i;
            email = e;
            brand = b;
            init();
        }
        public Record(string line)
        {
            if (line.Contains("\t"))
            {
                int fs = 1;
                foreach (string field in line.Split("\t".ToCharArray()))
                {
                    if (fs == 1)
                    {
                        id = field;
                    }
                    else if (fs == 2)
                    {
                        email = field;
                    }
                    else if (fs == 3)
                    {
                        brand = field;
                    }
                    fs++;
                }
            }
            init();
        }

        private void init()
        {
            parseDomain();
            setHeader();
        }

        private void parseDomain()
        {
            if (email.Contains("@"))
            {
                domain = email.Substring(email.IndexOf("@") + 1);
                baseDomain = domain;
                int period = baseDomain.IndexOf(".");
                int secondPeriod;
                while ((secondPeriod = baseDomain.IndexOf(".", period + 1)) > 0)
                {
                    baseDomain = baseDomain.Substring(period + 1);
                    period = baseDomain.IndexOf(".");
                }
            }
        }

        private void setHeader()
        {
            headerFields.Add("ID");
            headerFields.Add("EMAIL");
            headerFields.Add("BRAND");
            headerFields.Add("FINDING");
            headerFields.Add("COMMENT");
            headerFields.Add("COMMENT_CODE");
            headerFields.Add("SUGG_EMAIL");
            headerFields.Add("SUGG_COMMENT");
            headerFields.Add("DOMAIN");
            headerFields.Add("BASE DOMAIN");
            headerFields.Add("FA INTERNAL REASON");
            headerFields.Add("FA SUGGESTION SOURCE");
            headerFields.Add("MX STATUS");
            headerFields.Add("MX ERROR TYPE");
            headerFields.Add("FA_RID");
            headerFields.Add("VSN");
            headerFields.Add("UUID");
            headerFields.Add("SERVER TIME");
            headerFields.Add("ERROR");
        }

        public string output()
        {
            if (result == null)
            {
                result = new REACToutput(true);
            }

            //ensure matches setHeader fields
            outputFields.Add(id);
            outputFields.Add(email);
            outputFields.Add(brand);
            outputFields.Add(result.finding);
            outputFields.Add(result.comment);
            outputFields.Add(result.comment_code);
            outputFields.Add(result.sugg_email);
            outputFields.Add(result.sugg_comment);
            outputFields.Add(domain);
            outputFields.Add(baseDomain);
            outputFields.Add(result.fai_reason);
            outputFields.Add(result.fai_suggestion_source);
            outputFields.Add(result.fai_mx_status);
            outputFields.Add(result.fai_java_mx_error_type);
            outputFields.Add(result.fai_rid);
            outputFields.Add(result.fai_vsn);
            outputFields.Add(result.uuid);
            outputFields.Add(result.fai_servertime);
            outputFields.Add(result.error);
            return String.Join("\t", outputFields.ToArray());
        }

        public string outputHeader()
        {
            return String.Join("\t", headerFields.ToArray());
        }



    }

    public class REACToutput
    {
        //http://rt-local-test.freshaddress.biz:8080/react7?email=ajtest

        public string email = String.Empty;
        public string finding = String.Empty;
        public string comment = String.Empty;
        public string comment_code = String.Empty;
        public string sugg_email = String.Empty;
        public string sugg_comment = String.Empty;
        public string error = String.Empty;
        public string uuid = String.Empty;
        public string fai_reason = String.Empty;
        public string fai_suggestion_source = String.Empty;
        public string fai_mx_status = String.Empty;
        public string fai_java_mx_error_type = String.Empty;
        public string fai_rid = String.Empty;
        public string fai_vsn = String.Empty;
        public string fai_servertime = String.Empty;

        public DateTime REACTstart = DateTime.Now;
        public DateTime REACTend = DateTime.Now;
        public double runtime = 0;

        public REACToutput() { }
        public REACToutput(bool IsMissingResponse)
        {
            if (IsMissingResponse == true)
            {
                this.finding = "X";
                this.comment_code = "X";
                this.comment = "missing validation";
                this.fai_reason = "REACT response missing";
            }
        }
    }

}
