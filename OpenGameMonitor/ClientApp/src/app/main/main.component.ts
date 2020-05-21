import { Component, Pipe, PipeTransform, OnInit, OnDestroy, ViewChild } from "@angular/core";
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Router, NavigationEnd, NavigationStart } from '@angular/router';
import { Location } from '@angular/common';
import { trigger, transition, style, animate } from '@angular/animations';

import { Observable, Subject, combineLatest } from 'rxjs';
import { map, shareReplay, filter, startWith, takeUntil } from 'rxjs/operators';

import { AuthorizeService } from '../../api-authorization/authorize.service';
import { EventService } from '../services/event.service';
import { appRoutes, RouteItem } from './main.module';
import { MatSidenavContainer } from '@angular/material/sidenav';

interface TabItem {
	title: string;
	path: string;
	shouldShow?: (() => boolean) | Observable<boolean> | Promise<boolean>;
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
	SidebarItems: TabItem[] = [
		{
			title: 'Home',
			path: 'home'
		},
		{
			title: 'Servers',
			path: 'servers'
		},
		{
			title: 'Settings',
			path: 'settings',
			shouldShow: this.auth.hasUserPermission("Settings.View")
		}
	];

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

	@ViewChild(MatSidenavContainer) sidenavContainer: MatSidenavContainer;

	constructor(
		private breakpointObserver: BreakpointObserver,
		private router: Router,
		private location: Location,
		private events: EventService,
		private auth: AuthorizeService
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

		return typeof routeFound != 'undefined';
	}

	filterItems(route: TabItem): boolean | Promise<boolean> | Observable<boolean> {
		if (route.shouldShow) {
			return typeof route.shouldShow == 'function' ? route.shouldShow() : route.shouldShow;
		}

		return true;
	}

	goBack(): void {
		this.location.back();
	}

	linkOpened(): void {
		if (this.breakpointObserver.isMatched(Breakpoints.Handset)) {
			this.sidenavContainer.close();
		}
	}
}
