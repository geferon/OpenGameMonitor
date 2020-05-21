import { NgModule } from "@angular/core";
import { RouterModule, Routes, Route } from '@angular/router';
import { PermissionGuard } from '../utils/permission.guard';
// import { HomeComponent } from './home/home.component';

export interface RouteItem extends Route {
	main?: boolean;
	// children?: RouteItem[];
}

export const appRoutes: RouteItem[] = [
	{
		path: 'home',
		loadChildren: () => import('./home/home.module').then(mod => mod.HomeModule),
		main: true
	},
	{
		path: 'servers',
		loadChildren: () => import('./servers/servers.module').then(mod => mod.ServersModule)
	},
	{
		path: 'settings',
		loadChildren: () => import('./settings/settings.module').then(mod => mod.SettingsModule),
		canActivate: [PermissionGuard],
		data: { permissions: ["Settings.View"] }
	},
	// {
	// 	path: 'server-details',
	// 	loadChildren: () => import('./server-details/server-details.module').then(mod => mod.ServerDetailsModule)
	// }
];

// Sidebar items exporting
for (const item of appRoutes) {
	if (item.main) {
		const redirect = {
			path: '',
			pathMatch: 'full',
			redirectTo: item.path
		} as RouteItem;
		appRoutes.push(redirect);
		break;
	}
}

@NgModule({
	imports: [
		RouterModule.forChild(
			appRoutes
		)
	],
	declarations: []
})
export class MainModule {
}
