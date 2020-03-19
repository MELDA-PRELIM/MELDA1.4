using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

namespace WindowsFormsApplication5
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            /*
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                string SourceName = "WindowsService.ExceptionLog";
                if (!EventLog.SourceExists(SourceName))
                {
                    EventLog.CreateEventSource(SourceName, "Application");
                }

                EventLog eventLog = new EventLog();
                eventLog.Source = SourceName;
                string message = string.Format("Exception: {0} \n\nStack: {1}", ex.Message, ex.StackTrace);
                eventLog.WriteEntry(message, EventLogEntryType.Error);
            }*/
        }
    }
}
