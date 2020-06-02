import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerUpdateComponent } from './server-update.component';
import { RouterModule } from '@angular/router';
import { ComponentsModule } from '../../../components/components.module';


@NgModule({
	declarations: [ServerUpdateComponent],
	imports: [
		CommonModule,
		ComponentsModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerUpdateComponent
			}
		])
	]
})
export class ServerUpdateModule { }
