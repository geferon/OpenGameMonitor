import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerConsoleComponent } from './server-console.component';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';



@NgModule({
	declarations: [ServerConsoleComponent],
	imports: [
		CommonModule,
		MaterialModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerConsoleComponent
			}
		])
	]
})
export class ServerConsoleModule { }
