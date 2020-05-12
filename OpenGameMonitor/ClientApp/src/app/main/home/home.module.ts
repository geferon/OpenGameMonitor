import { NgModule } from "@angular/core";
import { CommonModule } from '@angular/common';
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home.component';
import { MaterialModule } from '../../material.module';

const routes: Routes = [
	{
		path: '',
		// pathMatch: 'full',
		component: HomeComponent
	}
];

@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		RouterModule.forChild(routes)
	],
	declarations: [
		HomeComponent
	],
	exports: [
		HomeComponent
	]
})
export class HomeModule {
	constructor() {

	}
}
