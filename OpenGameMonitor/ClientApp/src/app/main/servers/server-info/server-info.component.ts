import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, BehaviorSubject, Subject, from } from 'rxjs';
import { filter, takeUntil, mergeMap } from 'rxjs/operators';

import { ServerService } from '../../../services/server.service';
import { EventService } from '../../../services/event.service';
import { Server, MonitorUser, ProcessPriorityClass, ProcessStatus, ServerResourceMonitoringRegistry } from '../../../definitions/interfaces';

@Component({
	selector: 'app-server-info',
	templateUrl: './server-info.component.html',
	styleUrls: ['./server-info.component.scss']
})
export class ServerInfoComponent implements OnInit, OnDestroy {

	constructor(
		private route: ActivatedRoute,
		private router: Router,
		//private dialog: MatDialog,
		private snackBar: MatSnackBar,
		private servers: ServerService,
		private events: EventService
	) { }

	public Id: number;
	public Server$: Observable<Server>;
	public Registries$ = new BehaviorSubject<ServerResourceMonitoringRegistry[]>([]);

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');

		from(this.servers.subscribeToServer(this.Id))
			.pipe(
				mergeMap(() => this.servers.getServersRecordsAdded()),
				takeUntil(this.onDestroy),
				filter(([serverId, record]: [number, object]) => {
					return serverId == this.Id;
				})
			)
			.subscribe(([serverId, record]: [number, object]) => {
				console.log(record);
			});

		this.fetchDetails();
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		let subs = this.servers.unsubscribeFromServers().subscribe(() => subs.unsubscribe());
	}

	private fetchDetails() {
		this.Server$ = this.servers.getServer(this.Id);
		this.servers.getServersResourceMonitoringRegistries(this.Id).subscribe(regs => this.Registries$.next(regs));
	}

}
