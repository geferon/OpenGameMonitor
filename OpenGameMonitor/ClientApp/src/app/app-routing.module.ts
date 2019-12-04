import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { MaterialModule } from './material.module';
import { AuthorizeGuard } from '../api-authorization/authorize.guard';
// import { NoAuthGuardService, AuthGuardService } from './components/auth/auth.service';

import { HomeComponent } from './main/home/home.component';
import { CounterComponent } from './main/counter/counter.component';
import { FetchDataComponent } from './main/fetch-data/fetch-data.component';
import { MainComponent, ValidateRoutePipe } from './main/main.component';
import { ApiAuthorizationModule } from '../api-authorization/api-authorization.module';


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

