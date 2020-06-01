import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ServerConsoleComponent } from './server-console/server-console.component';
import { MaterialModule } from '../material.module';



@NgModule({
	declarations: [ServerConsoleComponent],
	imports: [
		CommonModule,
		MaterialModule,
		ScrollingModule,
	],
	exports: [
		ServerConsoleComponent
	]
})
export class ComponentsModule { }
