import { MaterialModule } from './material.module';
import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';


// import { NoAuthGuardService, AuthGuardService } from './components/auth/auth.service';

import { HomeComponent } from './main/home/home.component';
import { CounterComponent } from './main/counter/counter.component';
import { FetchDataComponent } from './main/fetch-data/fetch-data.component';
import { MainComponent, ValidateRoutePipe } from './main/main.component';


const appRoutes: Routes = [
	{
		path: '',
		redirectTo: 'main',
		pathMatch: 'full'
	},
	{
		path: 'main',
		component: MainComponent,
		loadChildren: () => import('./main/main.module').then(mo => mo.MainModule),
		canActivate: [AuthorizeGuard]
	},
	{
		path: 'login',
		// TODO
	}
];

@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		RouterModule.forRoot(
			appRoutes,
			{enableTracing: true}
		)
	],
	declarations: [MainComponent, ValidateRoutePipe],
	exports: [RouterModule, MainComponent, ValidateRoutePipe],
	// providers: [NoAuthGuardService, AuthGuardService]
})
export class AppRoutingModule {
	constructor() { }
}

