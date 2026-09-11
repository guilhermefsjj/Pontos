using System;
using System.Windows;

namespace Pontos
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            System.Diagnostics.Debug.WriteLine("[INFO] Application starting");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[INFO] Application closing");
            base.OnExit(e);
        }
    }
}
