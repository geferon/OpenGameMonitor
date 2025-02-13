import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';

import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { LayoutModule } from '@angular/cdk/layout';
import { FlexLayoutModule } from '@angular/flex-layout';
import { MaterialModule } from './material.module';

import { ApiAuthorizationModule } from '../api-authorization/api-authorization.module';
import { AuthorizeGuard } from '../api-authorization/authorize.guard';
import { AuthorizeInterceptor } from '../api-authorization/authorize.interceptor';
import { DialogsModule } from './main/dialogs/dialogs.module';
import { ChartsModule } from 'ng2-charts';
import { ComponentsModule } from './components/components.module';

@NgModule({
	declarations: [
		AppComponent,

		NavMenuComponent
	],
	imports: [
		BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
		HttpClientModule,

		FormsModule,
		ApiAuthorizationModule,
		AppRoutingModule,

		BrowserAnimationsModule,
		LayoutModule,
		MaterialModule,
		FlexLayoutModule,
		ChartsModule,

		DialogsModule,
		ComponentsModule
	],
	providers: [
		{ provide: HTTP_INTERCEPTORS, useClass: AuthorizeInterceptor, multi: true }
	],
	bootstrap: [AppComponent]
})
export class AppModule { }
