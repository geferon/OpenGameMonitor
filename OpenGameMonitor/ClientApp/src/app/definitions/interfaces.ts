
interface Trackable {
	Inserted: Date;
	Updated: Date;
}

export enum ProcessPriorityClass {
	Normal = 32,
	Idle = 64,
	High = 128,
	RealTime = 256,
	BelowNormal = 16384,
	AboveNormal = 32768
}

export enum ProcessStatus {
	Stopped = 1,
	Started = 2,
	//Starting = 3,
	Updating = 4
}

export class ServerEnvironmentVariable {
	Key: string;
	Value: string;
}

export class Server implements Trackable {
	Id: number;
	Name: string;
	Owner: MonitorUser;
	Group: Group;
	Enabled: boolean = true;

	Executable: string;
	Path: string;
	Graceful: boolean = true;
	RestartOnClose: boolean = true;

	StartParams?: string;
	StartParamsHidden?: string;
	StartParamsModifyAllowed: boolean = true;
	ProcessPriority: ProcessPriorityClass = ProcessPriorityClass.Normal;
	ProcessStatus: ProcessStatus = ProcessStatus.Stopped;
	EnvironmentVariables: ServerEnvironmentVariable[] = [];

	IP?: string;
	DisplayIP?: string;
	Port: number;

	Game: Game;
	Branch?: string;
	BranchPassword?: string;

	PID?: number;
	UpdatePID?: number;
	LastStart?: Date;

	LastUpdate?: Date;
	LastUpdateFailed: boolean = false;

	Inserted: Date;
	Updated: Date;
}

export class Game {
	Id: string;
	Name: string;
	Engine: string;
	SteamID: number;
}

export class MonitorUser {
	Id: string;
	UserName: string;
	Email: string;
	Password?: string;

	Groups: GroupUser[];
	Roles: MonitorRole[];
}

export type MonitorRole = string;
// export class MonitorRole {
// 	Id: string;
// 	Name: string;
// }

export class Group implements Trackable {
	Id: number;
	Name: string;

	Members: GroupUser[];

	Inserted: Date;
	Updated: Date;
}

export class GroupUser {
	UserID: string;
	User: MonitorUser;
	GroupID: number;
	Group: Group;
}

export class Setting {
	Key: string;
	Value: any;
}

