import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FlexLayoutModule } from '@angular/flex-layout';
import { ChartsModule } from 'ng2-charts';
import { ServerInfoComponent } from './server-info.component';
import { MaterialModule } from '../../../material.module';
import { ComponentsModule } from '../../../components/components.module';



@NgModule({
	declarations: [ServerInfoComponent],
	imports: [
		CommonModule,
		MaterialModule,
		FlexLayoutModule,
		ComponentsModule,
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
