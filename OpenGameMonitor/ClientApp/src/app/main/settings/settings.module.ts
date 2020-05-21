import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SettingsComponent } from './settings.component';
import { MaterialModule } from '../../material.module';
import { RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';


@NgModule({
	declarations: [SettingsComponent],
	imports: [
		CommonModule,
		MaterialModule,
		FormsModule,
		ReactiveFormsModule,
		RouterModule.forChild([{
			path: '',
			component: SettingsComponent
		}])
	]
})
export class SettingsModule { }
