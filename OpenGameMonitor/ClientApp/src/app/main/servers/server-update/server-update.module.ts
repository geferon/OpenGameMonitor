import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerUpdateComponent } from './server-update.component';
import { RouterModule } from '@angular/router';



@NgModule({
	declarations: [ServerUpdateComponent],
	imports: [
		CommonModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerUpdateComponent
			}
		])
	]
})
export class ServerUpdateModule { }
