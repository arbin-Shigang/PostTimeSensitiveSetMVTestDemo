using ArbinCTI.Core;
using ArbinCTI.Core.Control;
using ArbinCTI.Core.Inteface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PostTimeSensitiveSetMVDemo
{
    class Program
    {
        private static ArbinClient client;
        private static MyArbinControl ctrl;
        static MyTestData testData;
        private static bool? connected;

        private static string MyIP = "192.168.1.1";
        private static string UserName = "admin";
        private static string Password = "000000";
        private static int IVCount = 0;

        static FileStream fs = new FileStream($"C:/TestingCTIInterfaceSpeed/{nameof(PostTimeSensitiveSetMV)}.csv", FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        static StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);

        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(cancelHandler, true);
            Console.WriteLine("Enter a valid IP(xxx.xxx.xxx.xxx):");

            while (true)
            {
                string ip = Console.ReadLine();

                if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out IPAddress iPAddress))
                {
                    MyIP = iPAddress.ToString();
                    break;
                }

                Console.WriteLine("Please enter a valid IP(xxx.xxx.xxx.xxx)");
            }

            Console.WriteLine("Number of input channels:");

            while (true)
            {
                string count = Console.ReadLine();

                if (!string.IsNullOrEmpty(count) && int.TryParse(count, out int nCount))
                {
                    IVCount = nCount;
                    break;
                }

                Console.WriteLine("Input valid number of channels");
            }

            InitializeTests();

            Console.WriteLine("Start");

            PostTimeSensitiveSetMV();

            CloseFile();

            Console.WriteLine("End");
        }

        public delegate bool ControlCtrlDelegate(int CtrlType);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);
        private static ControlCtrlDelegate cancelHandler = new ControlCtrlDelegate(HandlerRoutine);

        public static bool HandlerRoutine(int CtrlType)
        {
            switch (CtrlType)
            {
                case 0:
                case 2:
                    {
                        CloseFile();
                    }
                    break;
            }

            return true;
        }

        private static void CloseFile()
        {
            sw.Flush();
            sw.Close();
            sw.Dispose();
            fs.Close();
            fs.Dispose();
        }

        static void InitializeTests()
        {
            testData = new MyTestData();
            ctrl = new MyArbinControl(testData);
            ctrl.Start();
            client = new ArbinClient();
            client.OnConnectionChanged += (IArbinSocket Socket, ArbinSocketEventArgSet.SocketConnectionEventArgs e) =>
            {
                connected = e.Connected;
            };

            client.OnSocketInfoLogCall += (IArbinSocket Socket, ArbinSocketEventArgSet.SocketErrorEventArgs e) =>
            {
                if (e.IsError)
                {
                    throw new Exception(e.Msg);
                }
            };

            ctrl.ListenSocketRecv(client);
            client.ConnectAsync(MyIP, 9031, 0, out int err);
            while (true)
            {
                if (connected.HasValue && connected.Value)
                {
                    testData.LoginFeed = null;
                    ctrl.PostLogicConnect(client, true);
                    ctrl.PostUserLogin(client, UserName, Password);
                    break;
                }
            }
        }
        static void PostTimeSensitiveSetMV()
        {
            while (true)
            {
                if (testData.LoginFeed != null)
                {
                    break;
                }
            }

            TimeSensitiveSetMVArgs args = new TimeSensitiveSetMVArgs();

            int nLoodIdx = 0;

            var fCurrent = 0.0f;
            var fVoltage = 0.0f;
            var fMVValue = 0.05f;
            TimeSensitiveSetMV mv = null;

            int ID = nLoodIdx + 1;
            int nHour = 0;
            int nCurrentHour = 0;

            long startDateTime = 0;
            long endDateTime = 0;
            float fSubTime = 0.0f;

            for (int nChannelIdx = 0; nChannelIdx < IVCount; nChannelIdx++)
            {
                args.Channels.Add(new TimeSensitiveSetMVArgs.TimeSensitiveSetMVChannel(nChannelIdx, new List<TimeSensitiveSetMV>()
                {
                    new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD1, Value = 0.0f },
                    new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD2, Value = fMVValue },
                    new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD3, Value = ID },
                    new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD4, Value = fCurrent },
                    new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD5, Value = fVoltage },
                }));
            }

            Console.WriteLine($"ID: {ID}");

            while (true)
            {
                if (ID >= 1036800000)
                {
                    break;
                }

                nCurrentHour = ID / 720000;
                if (nCurrentHour != nHour)
                {
                    Console.WriteLine($"ID: {ID}");
                    nHour = nCurrentHour;
                }

                Thread.Sleep(1);

                startDateTime = DateTime.UtcNow.Ticks;

                ctrl.PostTimeSensitiveSetMV(client, args);

                args.Channels.Clear();

                while (true)
                {
                    if (testData.TimeSensitiveSetMVFeed != null)
                    {
                        endDateTime = DateTime.UtcNow.Ticks;
                        ID = nLoodIdx + 1;

                        fMVValue = -fMVValue;
                        fSubTime = (endDateTime - startDateTime) / TimeSpan.TicksPerMillisecond;

                        for (int nChannelIdx = 0; nChannelIdx < IVCount; nChannelIdx++)
                        {
                            var chan = testData.TimeSensitiveSetMVFeed.Results.Find(item => item.GlobalIndex == nChannelIdx);
                            if (chan != null)
                            {
                                fCurrent = chan.Current;
                                fVoltage = chan.Voltage;
                            }

                            args.Channels.Add(new TimeSensitiveSetMVArgs.TimeSensitiveSetMVChannel(nChannelIdx, new List<TimeSensitiveSetMV>()
                            {
                                new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD1, Value = fSubTime },
                                new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD2, Value = fMVValue },
                                new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD3, Value = ID },
                                new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD4, Value = fCurrent },
                                new TimeSensitiveSetMV() { MVUD = TimeSensitiveSetMV.EMVUD.MVUD5, Value = fVoltage },
                            }));
                        }

                        testData.TimeSensitiveSetMVFeed = null;
                        nLoodIdx++;

                        break;
                    }
                }
            }
        }

    }

    public class MyTestData
    {
        public ArbinCommandLoginFeed LoginFeed { get; set; }
        public ArbinCommandAssignScheduleFeed AssignScheduleFeed { get; set; }
        public ArbinCommandLogicConnectFeed LogicConnectFeed { get; set; }
        public ArbinCommandStartChannelFeed StartChannelFeed { get; set; }
        public ArbinCommandBrowseDirectoryFeed BrowseDirectoryFeed { get; set; }
        public ArbinCommandStopChannelFeed StopChannelFeed { get; set; }
        public ArbinCommandGetSerialNumberFeed GetSerialNumberFeed { get; set; }
        public ArbinCommandResumChanneleFeed ResumChanneleFeed { get; set; }
        public ArbinCommandNewOrDeleteFeed NewOrDeleteFeed { get; set; }
        public ArbinCommandJumpChannelFeed JumpChannelFeed { get; set; }
        public ArbinCommandGetStartDataFeed GetStartDataFeed { get; set; }
        public ArbinCommandGetChannelDataSimpleModeFeed GetChannelDataSimpleModeFeed { get; set; }
        public ArbinCommandGetChannelDataMinimalistModeFeed GetChannelDataMinimalistModeFeed { get; set; }
        public ArbinCommandGetMetaVariablesFeed GetMetaVariablesFeed { get; set; }
        public ArbinCommandSetIntervalTimeLogDataFeed SetIntervalTimeLogDataFeed { get; set; }
        public ArbinCommandCheckFileExFeed CheckFileExFeed { get; set; }
        public ArbinCommandAssignFileFeed AssignFileFeed { get; set; }
        public ArbinCommandGetChannelDataFeed GetChannelDataFeed { get; set; }
        public ArbinCommandUpdateMetaVariableAdvancedFeed UpdateMetaVariableAdvanced { get; set; }
        public ArbinCommandUpLoadFileFeed UpLoadFileFeed { get; set; }

        public ArbinCommandUpdateParameterFeed UpdateParameterFeed { get; set; }

        public ArbinCommandConvertToAnonymousOrNamedTOFeed ConvertToAnonymousOrNamedTOFeed { get; set; }

        public ArbinCommandGetChannelInfoExFeed GetChannelInfoExFeed { get; set; }

        public ArbinCommandGetResumeDataFeed GetResumeDataFeed { get; set; }

        public ArbinCommandSetMetaVariableFeed SetMetaVariableFeed { get; set; }

        public ArbinCommandGetStringLimitLengthFeed GetStringLimitLengthFeed { get; set; }

        public ArbinCommandGetServerSoftwareVersionNumberFeed GetServerSoftwareVersionNumberFeed { get; set; }
        public ArbinCommandTimeSensitiveSetMVFeed TimeSensitiveSetMVFeed { get; internal set; } = null;
    }

    public class MyArbinControl : ArbinControl
    {
        public MyTestData TestData { get; }

        public MyArbinControl(MyTestData testData)
        {
            TestData = testData;
        }

        public override void OnAssignScheduleFeedBack(ArbinCommandAssignScheduleFeed cmd)
        {
            TestData.AssignScheduleFeed = cmd;
        }

        public override void OnBrowseDirectoryBack(ArbinCommandBrowseDirectoryFeed cmd)
        {
            TestData.BrowseDirectoryFeed = cmd;
        }



        public override void OnDownLoadFileBack(ArbinCommandDownLoadFileFeed cmd)
        {

        }

        public override void OnGetChannelsDataFeedBack(ArbinCommandGetChannelDataFeed cmd)
        {
            TestData.GetChannelDataFeed = cmd;
        }

        public override void OnGetResumeDataBack(ArbinCommandGetResumeDataFeed cmd)
        {
            TestData.GetResumeDataFeed = cmd;
        }

        public override void OnGetSerialNumberFeedBack(ArbinCommandGetSerialNumberFeed cmd)
        {
            TestData.GetSerialNumberFeed = cmd;
        }

        public override void OnGetStartDataBack(ArbinCommandGetStartDataFeed cmd)
        {
            TestData.GetStartDataFeed = cmd;
        }

        public override void OnJumpChannelFeedBack(ArbinCommandJumpChannelFeed cmd)
        {
            TestData.JumpChannelFeed = cmd;
        }

        public override void OnLogicConnectFeedBack(ArbinCommandLogicConnectFeed cmd)
        {
            TestData.LogicConnectFeed = cmd;
        }

        public override void OnNewFolderBack(ArbinCommandNewFolderFeed cmd)
        {

        }

        public override void OnNewOrDeleteBack(ArbinCommandNewOrDeleteFeed cmd)
        {
            TestData.NewOrDeleteFeed = cmd;
        }

        public override void OnResumeChannelFeedBack(ArbinCommandResumChanneleFeed cmd)
        {
            TestData.ResumChanneleFeed = cmd;
        }

        public override void OnSendMsgToCTIBack(ArbinCommandSendMsgToCTIFeed cmd)
        {

        }

        public override void OnSetMetaVariableFeedBack(ArbinCommandSetMetaVariableFeed cmd)
        {
            TestData.SetMetaVariableFeed = cmd;
        }

        public override void OnStartAutomaticCalibrationBack(ArbinCommandStartAutomaticCalibrationFeed cmd)
        {

        }

        public override void OnStartChannelFeedBack(ArbinCommandStartChannelFeed cmd)
        {
            TestData.StartChannelFeed = cmd;
        }

        public override void OnStopChannelFeedBack(ArbinCommandStopChannelFeed cmd)
        {
            TestData.StopChannelFeed = cmd;
        }

        public override void OnUpdateMetaVariableAdvancedFeedBack(ArbinCommandUpdateMetaVariableAdvancedFeed cmd)
        {
            TestData.UpdateMetaVariableAdvanced = cmd;
        }

        public override void OnUpLoadFileBack(ArbinCommandUpLoadFileFeed cmd)
        {
            TestData.UpLoadFileFeed = cmd;
        }

        public override void OnUserLoginFeedBack(ArbinCommandLoginFeed cmd)
        {
            TestData.LoginFeed = cmd;
        }

        public override void OnContinueChannelFeedBack(ArbinCommandContinueChannelFeed cmd)
        {

        }

        public override void OnDeleteFileBack(ArbinCommandDeleteFileFeed cmd)
        {

        }

        public override void OnGetStatusExtendInformationBack(ArbinCommandGetChannelInfoExFeed cmd)
        {
            TestData.GetChannelInfoExFeed = cmd;
        }

        public override void OnAssignFileFeedBack(ArbinCommandAssignFileFeed cmd)
        {
            TestData.AssignFileFeed = cmd;
        }

        [Obsolete]
        public override void OnCheckFileBack(ArbinCommandCheckFileFeed cmd)
        {

        }

        public override void OnConvertToAnonymousOrNamedTOBack(ArbinCommandConvertToAnonymousOrNamedTOFeed cmd)
        {
            TestData.ConvertToAnonymousOrNamedTOFeed = cmd;
        }

        public override void OnUpdateParametersBack(ArbinCommandUpdateParameterFeed cmd)
        {
            TestData.UpdateParameterFeed = cmd;
        }

        public override void OnGetMetaVariablesFeedBack(ArbinCommandGetMetaVariablesFeed cmd)
        {
            TestData.GetMetaVariablesFeed = cmd;
        }

        public override void OnGetChannelsDataSimpleModeFeedBack(ArbinCommandGetChannelDataSimpleModeFeed cmd)
        {
            TestData.GetChannelDataSimpleModeFeed = cmd;
        }

        public override void OnGetChannelsDataMinimalistModeFeedBack(ArbinCommandGetChannelDataMinimalistModeFeed cmd)
        {
            TestData.GetChannelDataMinimalistModeFeed = cmd;
        }

        public override void OnSetIntervalTimeLogDataFeedBack(ArbinCommandSetIntervalTimeLogDataFeed cmd)
        {
            TestData.SetIntervalTimeLogDataFeed = cmd;
        }

        public override void OnApplyForUDPCommunicationFeedBack(ArbinCommandApplyForUDPCommunicationFeed cmd)
        {

        }

        public override void OnCheckFileExBack(ArbinCommandCheckFileExFeed cmd)
        {
            TestData.CheckFileExFeed = cmd;
        }

        public override void OnUpdateMetaVariableAdvancedExFeedBack(ArbinCommandUpdateMetaVariableAdvancedExFeed cmd)
        {

        }

        public override void OnUnknownCommandFeedBack(ArbinCommandUnknownCommandFeed cmd)
        {

        }

        public override void OnGetTChamberMappingInfoFeedBack(ArbinCommandGetTChamberMappingInfoFeed cmd)
        {

        }

        public override void OnAssignBarcodeInfoFeedBack(ArbinCommandAssignBarcodeInfoFeed cmd)
        {

        }

        public override void OnGetChannelsDataSPTTFeedBack(ArbinCommandGetChannelDataSPTTFeed cmd)
        {
        }

        public override void OnGetBarcodeInfoFeedBack(ArbinCommandGetBarcodeInfoFeed cmd)
        {
        }

        public override void OnGetMachineTypeFeedBack(ArbinCommandGetMachineTypeFeed cmd)
        {
        }

        public override void OnGetTrayStatusFeedBack(ArbinCommandGetTrayStatusFeed cmd)
        {
        }

        public override void OnEngageTrayFeedBack(ArbinCommandEngageTrayFeed cmd)
        {
        }

        public override void OnGetStringLimitLengthFeedBack(ArbinCommandGetStringLimitLengthFeed cmd)
        {
            TestData.GetStringLimitLengthFeed = cmd;
        }

        public override void OnGetServerSoftwareVersionNumberFeedBack(ArbinCommandGetServerSoftwareVersionNumberFeed cmd)
        {
            TestData.GetServerSoftwareVersionNumberFeed = cmd;
        }

        public override void OnTimeSensitiveSetMVFeedBack(ArbinCommandTimeSensitiveSetMVFeed cmd)
        {
            TestData.TimeSensitiveSetMVFeed = cmd;
        }
        private string ToTimeString(double dValue)
        {
            return DateTime.FromOADate(dValue).ToString("HH:mm:ss.fffff");
        }
    }

}
