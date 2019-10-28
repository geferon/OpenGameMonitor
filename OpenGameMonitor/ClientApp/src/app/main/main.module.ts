import { NgModule } from "@angular/core";
import { RouterModule, Routes, Route } from '@angular/router';
//import { HomeComponent } from './home/home.component';

export interface RouteItem extends Route {
	title?: string;
	main?: boolean;
	// children?: RouteItem[];
}

export const appRoutes: RouteItem[] = [
	{
		title: 'Home',
		path: 'home',
		//component: HomeComponent,
		loadChildren: './home/home.module.ts#HomeModule',
		main: true
	}
];

// Sidebar items exporting
for (let item of appRoutes) {
	if (item.main) {
		let redirect = {
			path: '',
			pathMatch: 'full',
			redirectTo: item.path
		} as RouteItem;
		appRoutes.push(redirect);
		break;
	}
}

console.log(appRoutes);

@NgModule({
	imports: [
		RouterModule.forChild(
			appRoutes
		)
	]
})
export class MainModule {
}
