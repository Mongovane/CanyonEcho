using System;
using System.Threading.Tasks;
using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using CanyonEcho_Wpf.Api;
using CanyonEcho_Wpf.Constants;
using CanyonEcho_Wpf.Helper;
using CanyonEcho_Wpf.ViewModels;
using CanyonEcho_Wpf.Views;

namespace CanyonEcho_Wpf
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        public App()
        {
            try
            {
                this.Startup += App_Startup;

                // WPF 程序全局异常捕获，防止崩溃退出
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }
            catch (Exception e)
            {
                e.Show();
            }
        }

        #region 只运行一个程序
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:删除未读的私有成员", Justification = "<挂起>")]
        private System.Threading.Mutex mutex;
        private async void App_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                mutex = new System.Threading.Mutex(true, "CanyonEcho_Wpf", out bool ret);

                if (!ret)
                {
                    var handle = Win32Helper.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, Titles.Main);
                    IPCHelper.SendMessage("显示", handle);
                    Environment.Exit(0);
                    return;
                }

                // 已移除原版明文 HTTP 远端统计上报，保护用户隐私并避免启动阻塞
            }
            catch (ApiException ex)
            {
                LogHelper.Log.Warn("远端统计服务不可用，已忽略", ex);
            }
            catch (Exception ex)
            {
                LogHelper.Log.Warn("启动统计检查异常，已忽略", ex);
            }
        }
        #endregion

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<WordsLibrarySet, WordsLibrarySetViewModel>();
            containerRegistry.RegisterDialog<WordEdit, WordEditViewModel>();
            containerRegistry.RegisterDialog<AiSettingsView, AiSettingsViewModel>();
        }

        #region 异常处理
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            ExceptionHandler(e.Exception.InnerException ?? e.Exception);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ExceptionHandler(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionHandler(e.ExceptionObject as Exception);
        }

        private void ExceptionHandler(Exception ex)
        {
            if (ex == null) return;
            Dispatcher.Invoke(() =>
            {
                ex.Show();
            });
        }
        #endregion
    }
}
