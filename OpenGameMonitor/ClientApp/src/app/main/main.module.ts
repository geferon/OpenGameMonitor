import { NgModule } from "@angular/core";
import { RouterModule, Routes, Route } from '@angular/router';
// import { HomeComponent } from './home/home.component';

export interface RouteItem extends Route {
	title?: string;
	main?: boolean;
	// children?: RouteItem[];
}

export const appRoutes: RouteItem[] = [
	{
		title: 'Home',
		path: 'home',
		// component: HomeComponent,
		loadChildren: () => import('./home/home.module').then(mod => mod.HomeModule),
		main: true
	}
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
	]
})
export class MainModule {
}
