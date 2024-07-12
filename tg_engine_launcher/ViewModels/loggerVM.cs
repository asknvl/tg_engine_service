using Avalonia.Threading;
using logger;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;

namespace tg_engine_launcher.ViewModels
{
    public class loggerVM : ViewModelBase, ILogger
    {
        #region const
        string logFolder = "logs";
        #endregion

        #region vars
        Queue<LogMessage> logMessages = new Queue<LogMessage>();
        System.Timers.Timer timer = new System.Timers.Timer();
        System.Timers.Timer clearTimer = new System.Timers.Timer();
        string filePath;
        #endregion

        #region properties
        bool disableFileOutput;
        public bool DisableFileOutput
        {
            get => disableFileOutput;
            set
            {
                if (value)
                    timer.Stop();
                else
                    timer.Start();

                disableFileOutput = value;
            }
        }

        bool isFilterEnabled;
        public bool IsFilterEnabled
        {
            get => isFilterEnabled;
            set => this.RaiseAndSetIfChanged(ref isFilterEnabled, value);
        }

        string filter;
        public string Filter
        {
            get => filter;
            set
            {
                this.RaiseAndSetIfChanged(ref filter, value);
                var splt = filter.Replace(" ", "").ToLower().Split(";");
                FilterList = new List<string>(splt);
            }
        }

        List<string> filterList = new();
        public List<string> FilterList
        {
            get => filterList;
            set => this.RaiseAndSetIfChanged(ref filterList, value);
        }

        bool _err = true;
        public bool ERR
        {
            get => _err;
            set => this.RaiseAndSetIfChanged(ref _err, value);
        }

        bool _warn = true;
        public bool WARN
        {
            get => _warn;
            set => this.RaiseAndSetIfChanged(ref _warn, value);
        }

        bool _dbg;
        public bool DBG
        {
            get => _dbg;
            set => this.RaiseAndSetIfChanged(ref _dbg, value);
        }

        bool _inf = true;
        public bool INF
        {
            get => _inf;
            set => this.RaiseAndSetIfChanged(ref _inf, value);
        }

        public ObservableCollection<LogMessage> Messages { get; set; } = new();

        bool needScroll = true;
        public bool NeedScroll
        {
            get => needScroll;
            set => this.RaiseAndSetIfChanged(ref needScroll, value);
        }

        #endregion

        #region commands
        public ReactiveCommand<Unit, Unit> clearCmd { get; }
        #endregion

        public loggerVM()
        {
            #region commands
            clearCmd = ReactiveCommand.Create(() =>
            {
                Messages.Clear();
            });
            #endregion

            var fileDirectory = Path.Combine(Directory.GetCurrentDirectory(), logFolder);
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);

            filePath = Path.Combine(fileDirectory, $"bot.log");

            if (File.Exists(filePath))
                File.Delete(filePath);

            timer.Interval = 10000;
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            clearTimer = new System.Timers.Timer(12 * 60 * 60 * 1000);
            clearTimer.AutoReset = true;
            clearTimer.Elapsed += ClearTimer_Elapsed;
            clearTimer.Start();

        }

        private void ClearTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);

            }
            catch (Exception ex)
            {

            }
        }

        #region private
        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            appendLogFile();
        }

        void appendLogFile()
        {
            try
            {

                using (StreamWriter sw = File.AppendText(filePath))
                {
                    while (logMessages.Count > 0)
                    {
                        LogMessage message = logMessages.Dequeue();
                        if (message != null)
                            sw.WriteLine(message.ToString());
                    }
                }

            }
            catch (Exception ex)
            {

            }
        }
        #endregion

        #region helpers
        void post(LogMessage message)
        {

            var found = true;

            if (IsFilterEnabled)
            {
                var filtered = message.ToFiltered();
                found = (FilterList.Count > 0) ? FilterList.Any(x => filtered.Contains(x)) : true;
            }

            if (found)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Messages.Add(message);
                });
            }

            if (!DisableFileOutput)
                logMessages.Enqueue(message);
        }
        #endregion

        #region public
        public void dbg(string tag, string text)
        {
            if (DBG)
            {
                var message = new LogMessage(LogMessageType.dbg, tag, text);
                post(message);
            }
        }

        public void warn(string tag, string text)
        {
            if (WARN)
            {
                var message = new LogMessage(LogMessageType.warn, tag, text);
                post(message);
            }
        }

        public void err(string tag, string text)
        {
            if (ERR)
            {
                var message = new LogMessage(LogMessageType.err, tag, text);
                post(message);
            }
        }

        public void inf(string tag, string text)
        {
            if (INF)
            {
                var message = new LogMessage(LogMessageType.inf, tag, text);
                post(message);
            }
        }

        public void inf_urgent(string tag, string text)
        {
            var message = new LogMessage(LogMessageType.inf_urgent, tag, text);
            post(message);
        }

        public void user_input(string tag, string text)
        {
            var message = new LogMessage(LogMessageType.user_input, tag, text);
            post(message);
        }
        #endregion
    }
}
