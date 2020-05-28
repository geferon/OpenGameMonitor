import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ServersComponent } from './servers.component';
import { MaterialModule } from '../../material.module';



@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		RouterModule.forChild([
			{
				path: '',
				component: ServersComponent
			},
			{
				path: 'new',
				loadChildren: () => import('./server-details/server-details.module').then(m => m.ServerDetailsModule)
			},
			{
				path: ':id',
				children: [
					{
						path: '',
						pathMatch: 'full',
						loadChildren: () => import('./server-info/server-info.module').then(m => m.ServerInfoModule)
					},
					{
						path: 'details',
						loadChildren: () => import('./server-details/server-details.module').then(m => m.ServerDetailsModule)
					},
					{
						path: 'update',
						loadChildren: () => import('./server-update/server-update.module').then(m => m.ServerUpdateModule)
					},
					{
						path: 'console',
						loadChildren: () => import('./server-console/server-console.module').then(m => m.ServerConsoleModule)
					}
				]
			}
		])
	],
	declarations: [ServersComponent],
	exports: [ServersComponent]
})
export class ServersModule { }
