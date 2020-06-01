import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ChartsModule } from 'ng2-charts';
import { ServerInfoComponent } from './server-info.component';
import { MaterialModule } from '../../../material.module';



@NgModule({
	declarations: [ServerInfoComponent],
	imports: [
		CommonModule,
		MaterialModule,
		ChartsModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerInfoComponent
			}
		])
	]
})
export class ServerInfoModule { }
