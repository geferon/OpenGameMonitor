import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerDetailsComponent } from './server-details.component';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';



@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		FormsModule,
		ReactiveFormsModule,
		RouterModule.forChild([
			{
				path: '',
				redirectTo: 'new',
				pathMatch: 'full'
			},
			{
				path: 'new',
				component: ServerDetailsComponent
			},
			{
				path: ':id',
				component: ServerDetailsComponent
			}
		])
	],
	declarations: [ServerDetailsComponent],
	exports: [ServerDetailsComponent]
})
export class ServerDetailsModule { }
