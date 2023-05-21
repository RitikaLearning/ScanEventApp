using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;

namespace ScanEventApp
{
    
    public class ScanEvent
    {
        public int EventId { get; set; }
        public int ParcelId { get; set; }
        public string Type { get; set; }
        public DateTime CreatedDateTimeUtc { get; set; }
        public string StatusCode { get; set; }
        public string ScanDateTimeUtc { get; set; }
        public Device Device { get; set; }
        public User User { get; set; }

    }
     
    public class Device
    {
        public int DeviceTransactionId { get; set; }
        public int DeviceId { get; set; }
    }

    public class User
    {
        public string UserId { get; set; }
        public string CarrierId { get; set; }
        public string RunId { get; set; }
    }

    class Program
    {
        
        static string sWorkingDir = System.Configuration.ConfigurationManager.AppSettings["WorkingDir"];
        static string CurrentEventIdFilePath = sWorkingDir + "CurrentEventId.txt";
        static string ScannedEventIdFilePath = sWorkingDir + "ScannedEvents.txt";

        static string logFilePath = sWorkingDir + "log.txt";

        static TextWriterTraceListener fileListener = new TextWriterTraceListener(logFilePath);

      
         
        static async Task FetchScanEvents()
        {


            Console.WriteLine("Process Started"); // We can also do logging with Trace
            HttpClient httpClient = new HttpClient();

            Console.WriteLine("Reading Last Saved Event ID");

            string sLastEvent =ReadFromTextFile(CurrentEventIdFilePath);


            DeleteFile(ScannedEventIdFilePath);

            int iLastEventId = 0;

            if (!BlankStr(sLastEvent))
            {

                ScanEvent oScanEvent = JsonConvert.DeserializeObject<ScanEvent>(sLastEvent);

                iLastEventId = ToSafeInteger(oScanEvent.EventId);

            }


            Console.WriteLine ("Last Scanned Event ID: " + iLastEventId.ToString());

            string apiUrl = "http://localhost:3000/ScanEvents";
              
            //string apiUrl = "http://localhost:3000/ScanEvents?EventId=" + iLastEventId.ToString ();

            Console.WriteLine("Fetching 100 records after Event ID" + iLastEventId.ToString());
            string responseJson = await httpClient.GetStringAsync(apiUrl);
             
            List<ScanEvent> oEventList = JsonConvert.DeserializeObject<List<ScanEvent>>(responseJson);

            WriteToTextFile(ScannedEventIdFilePath, "{" + @"""ScanEvents""" + ": [");
            Dictionary<int, ScanEvent> mostRecentScanEvents = new Dictionary<int, ScanEvent>();
            Dictionary<int, DateTime> pickupTimes = new Dictionary<int, DateTime>();
            Dictionary<int, DateTime> deliveryTimes = new Dictionary<int, DateTime>();

            try {

                for (int icnt = 0; icnt < oEventList.Count; icnt++)
                {
                    //scan logic

                    Console.WriteLine("Scanning Event ID " + oEventList[icnt].EventId.ToString());

                    oEventList[icnt].ScanDateTimeUtc = DateTime.UtcNow.ToString();

                    string scanEvent = JsonConvert.SerializeObject(oEventList[icnt]);
                    string sEvent = JsonConvert.SerializeObject(oEventList[icnt]);

                    int iRet = WriteToTextFile(CurrentEventIdFilePath, scanEvent);


                    if (!mostRecentScanEvents.ContainsKey(oEventList[icnt].ParcelId) || oEventList[icnt].EventId > mostRecentScanEvents[oEventList[icnt].ParcelId].EventId)
                    {
                        mostRecentScanEvents[oEventList[icnt].ParcelId] = oEventList[icnt];
                    }

                    // Track pickup and delivery times
                    if (oEventList[icnt].Type == "PICKUP")
                    {
                        pickupTimes[oEventList[icnt].ParcelId] = oEventList[icnt].CreatedDateTimeUtc;
                    }
                    else if (oEventList[icnt].Type == "DELIVERY")
                    {
                        deliveryTimes[oEventList[icnt].ParcelId] = oEventList[icnt].CreatedDateTimeUtc;
                    }

                    if (icnt == 0)
                    {
                        AppendTextToFile(ScannedEventIdFilePath, sEvent);
                    }
                    else
                    {
                        AppendTextToFile(ScannedEventIdFilePath, "," + sEvent);
                    }

                    //update server back with time stamp of deliver/pickup, I have done it by writing into text file
                    // but abviously we can do it by POst request and saving the latest scanEvent to DB
                }
            
            }

            catch (Exception ex)
            {

                LogMessage(ex.Message.ToString(), 0);
            }
            finally
            {
                AppendTextToFile(ScannedEventIdFilePath, "] }");

            }
             
            Console.WriteLine ("My Pickup is" + pickupTimes);
            Console.WriteLine("My Delivery is" + deliveryTimes);

            Console.WriteLine("Process Finished.");
            
            
        }

        static void Main(string[] args)  
        {
            FetchScanEvents().Wait();
        }


        static bool BlankStr(object oValue)
        {
            try
            {
                if (string.IsNullOrEmpty((string)oValue))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }


        static int ToSafeInteger(object oValue)
        {

            int iRetVal = 0;
             

            try
            {
                string sValue = oValue.ToString();

                bool isValid = int.TryParse(sValue, out iRetVal);
                if (isValid != true)
                {
                    iRetVal = 0;
                }
            }

            catch (Exception)
            {
                return 0;
            }

            return iRetVal;
        }

         static string ReadFromTextFile(string v_sFilePath)
        {
            string sRetVal = "";

            try
            {
                if (File.Exists(v_sFilePath))
                {
                    var oSR = File.OpenText(v_sFilePath);

                    sRetVal = oSR.ReadToEnd();

                    oSR.Close();

                    oSR = null;
                }

                return sRetVal;
            }

            catch (Exception ex)
            {

                LogMessage(ex.Message.ToString(),0);

                
                return "";
            }

        }

        static void LogMessage(string sMsg, int iType)
         {

             Trace.Listeners.Add(fileListener);

             Trace.WriteLine(DateTime.Now + "-----------------------------------------------");
            if (iType == 0)
            {

                Trace.TraceError(sMsg);
            }

         }

        static void AppendTextToFile(string sFilePath, string sContents)
        {

            StreamWriter sw;
             

            if (!File.Exists(sFilePath))
            {
                using (FileStream fs = File.Create(sFilePath));
            }

            
            sw = File.AppendText(sFilePath);

            sw.WriteLine(sContents);

            sw.Close();

        }

        static int DeleteFile(string v_sFilePath)
        {

            try
            {
                if (File.Exists(v_sFilePath))
                {
                    File.Delete(v_sFilePath);
                }
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, 0);
                return 0;
            }

            return 1;
        }

        static int WriteToTextFile(string sFilePath, string sContent)
        {
            try
            {

                if (!File.Exists(sFilePath))
                {
                    using (FileStream fs = File.Create(sFilePath));
                }

                using (StreamWriter writer = new StreamWriter(sFilePath))
                {
                    writer.WriteLine(sContent);
                   
                }
            }

            catch (Exception ex)
            {

                LogMessage(ex.Message, 0);
                return 0;
                 
            }

            return 1;
        }

    }
}
