using EntityFrameworkCore.Triggers;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Listeners
{
    public class DbListener
    {
        public DbListener()
        {
            Triggers<Server>.Inserted += ServerInserted;
            Triggers<Server>.Updated += ServerUpdated;
            
        }

        public void ServerInserted(IInsertedEntry<Server> server)
        {

        }

        public void ServerUpdated(IUpdatedEntry<Server> server)
        {

        }
    }
}
