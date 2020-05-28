import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ServerConsoleComponent } from './server-console.component';
import { MaterialModule } from '../../../material.module';



@NgModule({
	declarations: [ServerConsoleComponent],
	imports: [
		CommonModule,
		MaterialModule,
		ScrollingModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServerConsoleComponent
			}
		])
	]
})
export class ServerConsoleModule { }
