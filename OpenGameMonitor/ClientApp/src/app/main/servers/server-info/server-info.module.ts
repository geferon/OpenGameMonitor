import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerInfoComponent } from './server-info.component';
import { RouterModule } from '@angular/router';



@NgModule({
	declarations: [ServerInfoComponent],
	imports: [
		CommonModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerInfoComponent
			}
		])
	]
})
export class ServerInfoModule { }
