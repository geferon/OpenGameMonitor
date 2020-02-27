import { NgModule, APP_INITIALIZER } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoginMenuComponent } from './login-menu/login-menu.component';
import { LoginComponent } from './login/login.component';
import { LogoutComponent } from './logout/logout.component';
import { RouterModule } from '@angular/router';
import { ApplicationPaths } from './api-authorization.constants';
import { HttpClientModule, HttpClient } from '@angular/common/http';
import { MaterialModule } from '../app/material.module';
import { AuthModule, OidcConfigService, OidcSecurityService, ConfigResult, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { switchMap } from 'rxjs/operators';

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
			deps: [OidcConfigService, HttpClient],
			multi: true,
			useFactory: (oidcConfigService: OidcConfigService, http: HttpClient) => {
				return () => {
					return http
					.get(ApplicationPaths.ApiAuthorizationClientConfigurationUrl)
					.pipe(
						switchMap(config => {
							config['stsServer'] = config['authority'];
							return oidcConfigService['loadUsingConfiguration'](config);
						})
					)
					.toPromise();
				};
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
			let redirectUrl = new URL(configResult.customConfig.redirect_uri);

			const config: OpenIdConfiguration = {
				stsServer: configResult.customConfig.stsServer,
				redirect_url: redirectUrl.origin,
				client_id: configResult.customConfig.client_id,
				scope: configResult.customConfig.scope,
				response_type: configResult.customConfig.response_type,
				silent_renew: true,
				silent_renew_url: `${configResult.customConfig.authority}/silent-renew.html`,
				log_console_debug_active: true,
				post_logout_redirect_uri: configResult.customConfig.post_logout_redirect_uri,
				post_login_route: configResult.customConfig.redirect_uri
			};

			this.oidcSecurityService.setupModule(config, configResult.authWellknownEndpoints);
		});
	}
}
