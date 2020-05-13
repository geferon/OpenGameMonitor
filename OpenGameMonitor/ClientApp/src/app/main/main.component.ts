import { Component, Pipe, PipeTransform, OnInit, OnDestroy } from "@angular/core";
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Router, NavigationEnd, NavigationStart } from '@angular/router';
import { Location } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';

import { Observable, Subject, combineLatest } from 'rxjs';
import { map, shareReplay, filter, startWith, takeUntil } from 'rxjs/operators';

import { EventService } from '../services/event.service';
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

const isValidString = function(variable?: string) {
	if (variable) {
		return true;
	}
	return false;
};
@Component({
	selector: 'app-main',
	templateUrl: './main.component.html',
	styleUrls: ['./main.component.scss'],
	animations: [
		trigger('loadingState', [
			transition(':enter', [
				style({
					'max-height': '0px'
				}),
				animate('300ms ease-in', style({
					'max-height': '5px'
				}))
			]),
			transition(':leave', [
				style({
					'max-height': '5px'
				}),
				animate('300ms ease-out', style({
					'max-height': '0px'
				}))
			]),
		])
	]
})
export class MainComponent implements OnInit, OnDestroy {
	SidebarItems = appRoutes;

	isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
		.pipe(
			map(result => result.matches),
			shareReplay()
		);

	isLoading$: Observable<boolean> = this.router.events
		.pipe(
			filter(ev => ev instanceof NavigationStart || ev instanceof NavigationEnd),
			map(ev => ev instanceof NavigationStart)
		);

	isLoadingExternally$ = new Subject<boolean>();

	get isLoadingInternal$() {
		return combineLatest([
			this.isLoading$,
			this.isLoadingExternally$
		])
		.pipe(
			map(([one, two]) => (one || two))
		);
	}

	isRoot$: Observable<boolean> = this.router.events
		.pipe(
			filter(ev => ev instanceof NavigationEnd),
			map((ev: NavigationEnd) => this.isURLRoot(ev.url)),
			startWith(this.isURLRoot(this.router.url))
		);

	private readonly onDestroy = new Subject();

	constructor(
		private breakpointObserver: BreakpointObserver,
		private router: Router,
		private location: Location,
		private events: EventService
	) { }

	ngOnInit(): void {
		this.events.on("Loading")
			.pipe(takeUntil(this.onDestroy))
			.subscribe(isLoading => this.isLoadingExternally$.next(isLoading));
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();
	}

	private isURLRoot(urlStr: string) {
		let url = urlStr.split('/');
		url.shift();

		if (url.length != 2 || url[0] != 'main') {
			return false;
		}

		let routeFound = appRoutes.find(route => url[1] == route.path);

		return this.filterRoute(routeFound);
	}

	filterRoute(route: RouteItem): boolean {
		return isValidString(route.title) && isValidString(route?.path);
	}

	goBack(): void {
		this.location.back();
	}
}
