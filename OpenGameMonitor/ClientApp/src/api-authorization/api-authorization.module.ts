import { NgModule, APP_INITIALIZER } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoginMenuComponent } from './login-menu/login-menu.component';
import { LoginComponent } from './login/login.component';
import { LogoutComponent } from './logout/logout.component';
import { RouterModule } from '@angular/router';
import { ApplicationPaths } from './api-authorization.constants';
import { HttpClientModule } from '@angular/common/http';
import { MaterialModule } from '../app/material.module';
import { AuthModule, OidcConfigService, OidcSecurityService, ConfigResult } from 'angular-auth-oidc-client';

@NgModule({
	imports: [
		CommonModule,
		HttpClientModule,
		MaterialModule,
		RouterModule.forChild(
			[
				{ path: ApplicationPaths.Register, component: LoginComponent },
				{ path: ApplicationPaths.Profile, component: LoginComponent },
				{ path: ApplicationPaths.Login, component: LoginComponent },
				{ path: ApplicationPaths.LoginFailed, component: LoginComponent },
				{ path: ApplicationPaths.LoginCallback, component: LoginComponent },
				{ path: ApplicationPaths.LogOut, component: LogoutComponent },
				{ path: ApplicationPaths.LoggedOut, component: LogoutComponent },
				{ path: ApplicationPaths.LogOutCallback, component: LogoutComponent }
			]
		),
		AuthModule.forRoot()
	],
	declarations: [LoginMenuComponent, LoginComponent, LogoutComponent],
	exports: [LoginMenuComponent, LoginComponent, LogoutComponent],
	providers: [
		OidcConfigService,
		{
			provide: APP_INITIALIZER,
			deps: [OidcConfigService],
			multi: true,
			useFactory: (oidcConfigService: OidcConfigService) => {
				return () => oidcConfigService.load(ApplicationPaths.ApiAuthorizationClientConfigurationUrl);
			}
		}
	]
})
export class ApiAuthorizationModule {
	constructor(
		private oidcSecurityService: OidcSecurityService,
		private oidcConfigService: OidcConfigService
	) {
		// TODO: https://github.com/damienbod/angular-auth-oidc-client
		this.oidcConfigService.onConfigurationLoaded.subscribe((configResult: ConfigResult) => {

		});
	}
}
