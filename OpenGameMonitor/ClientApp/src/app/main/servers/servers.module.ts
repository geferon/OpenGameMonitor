import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ServersComponent } from './servers.component';
import { MaterialModule } from '../../material.module';



@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		RouterModule.forChild([{
			path: '',
			component: ServersComponent
		}])
	],
	declarations: [ServersComponent],
	exports: [ServersComponent]
})
export class ServersModule { }
