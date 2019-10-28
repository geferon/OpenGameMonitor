import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';


// import { NoAuthGuardService, AuthGuardService } from './components/auth/auth.service';

import { HomeComponent } from './main/home/home.component';
import { CounterComponent } from './main/counter/counter.component';
import { FetchDataComponent } from './main/fetch-data/fetch-data.component';
import { MainComponent } from './main/main.component';


const appRoutes: Routes = [
	{
		path: '',
		redirectTo: 'main',
		pathMatch: 'full'
	},
	{
		path: 'main',
		component: MainComponent
	},
];

@NgModule({
	imports: [
		RouterModule.forRoot(
			appRoutes
		)
	],
	exports: [RouterModule],
	// providers: [NoAuthGuardService, AuthGuardService]
})
export class AppRoutingModule {
	constructor() { }
}

