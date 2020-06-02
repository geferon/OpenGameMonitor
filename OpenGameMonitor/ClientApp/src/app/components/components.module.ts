import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ScrollingModule as ExperimentalScrollingModule } from '@angular/cdk-experimental/scrolling';
// import { VirtualScrollerModule } from 'ngx-virtual-scroller';
import { ServerConsoleComponent } from './server-console/server-console.component';
import { MaterialModule } from '../material.module';
import { ServerUpdateConsoleComponent } from './server-update-console/server-update-console.component';



@NgModule({
	declarations: [
		ServerConsoleComponent,
		ServerUpdateConsoleComponent
	],
	imports: [
		CommonModule,
		MaterialModule,
		ScrollingModule,
		// VirtualScrollerModule
		ExperimentalScrollingModule
	],
	exports: [
		ServerConsoleComponent,
		ServerUpdateConsoleComponent
	]
})
export class ComponentsModule { }
