using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorLibraries
{
    interface IMonitorComsInterface
    {
        Task<bool> ServerOpen(int server);
        Task<bool> ServerClose(int server);
        Task<bool> ServerUpdate(int server);
        Task ConfigReloaded();
        Task Connected();
    }

    interface IMonitorComsCallback
    {
        Task PanelConfigReloaded();
        Task ServerOpened(int server);
        Task ServerClosed(int server);
        Task ServerUpdated(int server);
    }
}
