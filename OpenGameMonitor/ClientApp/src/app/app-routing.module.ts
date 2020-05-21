import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { MaterialModule } from './material.module';
import { AuthorizeGuard } from '../api-authorization/authorize.guard';
// import { NoAuthGuardService, AuthGuardService } from './components/auth/auth.service';

import { HomeComponent } from './main/home/home.component';
import { MainComponent } from './main/main.component';
import { ApiAuthorizationModule } from '../api-authorization/api-authorization.module';
import { FilterPipe } from './utils/filter.pipe';
import { AppUtilsModule } from './utils/app-utils.module';


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
		BrowserAnimationsModule,
		MaterialModule,
		AppUtilsModule,
		RouterModule.forRoot(
			appRoutes,
			{enableTracing: true}
		)
	],
	declarations: [MainComponent],
	exports: [RouterModule, MainComponent],
	// providers: [NoAuthGuardService, AuthGuardService]
})
export class AppRoutingModule {
	constructor() { }
}

