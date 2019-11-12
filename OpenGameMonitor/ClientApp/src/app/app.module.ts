import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { CounterComponent } from './main/counter/counter.component';
import { FetchDataComponent } from './main/fetch-data/fetch-data.component';

import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { LayoutModule } from '@angular/cdk/layout';
import { MaterialModule } from './material.module';

import { ApiAuthorizationModule } from 'src/api-authorization/api-authorization.module';
import { AuthorizeGuard } from 'src/api-authorization/authorize.guard';
import { AuthorizeInterceptor } from 'src/api-authorization/authorize.interceptor';

@NgModule({
	declarations: [
		AppComponent,

		NavMenuComponent,

		CounterComponent,
		FetchDataComponent,
	],
	imports: [
		BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
		HttpClientModule,
		FormsModule,
		AppRoutingModule,
		BrowserAnimationsModule,
		LayoutModule,
		MaterialModule,
		ApiAuthorizationModule
	],
	providers: [
		{ provide: HTTP_INTERCEPTORS, useClass: AuthorizeInterceptor, multi: true }
	],
	bootstrap: [AppComponent]
})
export class AppModule { }
