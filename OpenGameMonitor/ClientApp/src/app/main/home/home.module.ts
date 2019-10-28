import { NgModule } from "@angular/core";
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home.component';
import { MaterialModule } from '../../material.module';

const routes: Routes = [
	{
		path: '',
		component: HomeComponent
	}
];

@NgModule({
	imports: [
		MaterialModule,
		RouterModule.forChild(routes)
	],
	declarations: [
		HomeComponent
	]
})
export class HomeModule {
	constructor() {

	}
}
