
interface ApiPathsType {
	readonly DefaultAPIPath: string;
	readonly Servers: string;
	readonly Users: string;
	readonly Groups: string;
	readonly Games: string;
	readonly Settings: string;
}

let apiBase = '/api'

let apiPaths: ApiPathsType = {
	DefaultAPIPath: apiBase,
	Servers: `${apiBase}/Servers`,
	Users: `${apiBase}/Users`,
	Groups: `${apiBase}/Groups`,
	Games: `${apiBase}/Games`,
	Settings: `${apiBase}/Settings`
};

export const APIPaths: ApiPathsType = apiPaths;
