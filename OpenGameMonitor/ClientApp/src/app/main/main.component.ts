import { Component, Pipe, PipeTransform } from "@angular/core";
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { appRoutes, RouteItem } from './main.module';

@Pipe({
	name: 'validateroute',
	pure: false
})

export class ValidateRoutePipe implements PipeTransform {
	transform(items: any[], filter: (item: any) => boolean): any {
		if (!items || !filter) {
			return items;
		}
		return items.filter(item => filter(item));
	}
}

@Component({
	selector: 'app-main',
	templateUrl: './main.component.html',
	styleUrls: ['./main.component.scss']
})
export class MainComponent {
	SidebarItems = appRoutes;

	isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
		.pipe(
			map(result => result.matches),
			shareReplay()
		);

	constructor(private breakpointObserver: BreakpointObserver) { }

	filterRoute(route: RouteItem): boolean {
		return (!(route.title === undefined || route.title === null) && (route.path === undefined || route.path === null));
	}
}
