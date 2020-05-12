using AutoMapper;
using OpenGameMonitorLibraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Utils
{
	public class AutoMapping : Profile
	{
		public AutoMapping()
		{
			CreateMap<MonitorRole, DTOMonitorRole>();
			CreateMap<DTOMonitorRole, MonitorRole>();

			CreateMap<MonitorUser, DTOMonitorUser>();
			CreateMap<DTOMonitorUser, MonitorUser>();
			CreateMap<DTOMonitorUserSend, MonitorUser>();

			CreateMap<Server, DTOServer>();
			CreateMap<DTOServer, Server>();
		}
	}
}
