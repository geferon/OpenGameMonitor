import { Component, OnInit, ViewChild, OnDestroy, SecurityContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DomSanitizer } from '@angular/platform-browser';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { SelectionModel } from '@angular/cdk/collections';

import { catchError, map, takeUntil, delay, finalize } from 'rxjs/operators';
import { Observable, of, BehaviorSubject, pipe, Subject, Subscription } from 'rxjs';

import { Server, Game, ProcessStatus } from '../../definitions/interfaces';
import { ServerService } from '../../services/server.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../dialogs/confirm-dialog/confirm-dialog.component';
import { EventService } from '../../services/event.service';
import { AuthorizeService } from '../../../api-authorization/authorize.service';

class ServerShowcase extends Server {
	LoadingStatus$: Observable<boolean>;
	LoadingProgress: number;
}

@Component({
	selector: 'app-servers',
	templateUrl: './servers.component.html',
	styleUrls: ['./servers.component.scss']
})
export class ServersComponent implements OnInit, OnDestroy {

	constructor(
		private dialog: MatDialog,
		private dom: DomSanitizer,
		private servers: ServerService,
		private events: EventService,
		private snack: MatSnackBar,
		private perms: AuthorizeService
	) { }

	@ViewChild(MatPaginator, { static: true }) paginator: MatPaginator;

	public Loading$ = new Subject<boolean>();
	public Errored = false;

	// public Servers: Server[] = [];
	public Servers$: BehaviorSubject<ServerShowcase[]> = new BehaviorSubject<ServerShowcase[]>([]);
	public ServersSource = new MatTableDataSource<ServerShowcase>([]);

	public CreatePermission$ = this.perms.hasUserPermission('Servers.Create');

	public ColumnsToDisplay = [
		"Select",
		"Name",
		"Owner",
		"Game",
		"Status",
		"IP",
		"Actions"
	];

	public ProcessStatus = ProcessStatus;

	public DetailsPerStatus = {
		[ProcessStatus.Started]: {
			Icon: 'play_arrow',
			Class: 'status-started',
			Hint: 'This server is currently runnning. Click to stop.'
		},
		[ProcessStatus.Stopped]: {
			Icon: 'stop',
			Class: 'status-stopped',
			Hint: 'This server is currently stopped. Click to start.'
		},
		[ProcessStatus.Updating]: {
			Icon: 'refresh',
			Class: 'status-updating',
			Hint: 'This server is currently updating. Click to view the update logs.'
		},
	};

	private readonly onDestroy = new Subject();

	public Selection: SelectionModel<ServerShowcase>;

	ngOnInit(): void {
		this.ServersSource.paginator = this.paginator;

		const initialSelection = [];
		const allowMultiSelect = true;
		this.Selection = new SelectionModel<ServerShowcase>(allowMultiSelect, initialSelection);

		this.Loading$
		.pipe(delay(0))
		.subscribe(loading => this.events.emit("Loading", loading));

		this.Servers$.subscribe(servers => {
			this.ServersSource.data = servers;
		});

		this.fetchData();
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();
	}

	fetchData() {
		this.Loading$.next(true);

		this.Errored = false;
		this.servers.getServersRealtime()
			.pipe(
				takeUntil(this.onDestroy),
				catchError((err, caught) => {
					console.error(err);
					this.Errored = true;

					return of([] as Server[]);
				}),
				map(s => s as ServerShowcase[])
			)
			.subscribe(servers => {
				this.Servers$.next(servers);
				this.Loading$.next(false);
			});

		this.servers.getServersUpdateProgress()
			.pipe(takeUntil(this.onDestroy))
			.subscribe(([serverId, progress]: [number, number]) => {
				let servers = this.Servers$.value;
				let server = servers.find(s => s.Id == serverId);

				server.LoadingProgress = progress;

				this.Servers$.next(servers);
			});
	}

	deleteServer(server: ServerShowcase) {
		let dialog = this.dialog.open(ConfirmDialogComponent, {
			data: {
				Title: "Do you want to delete this server?",
				Text: `Are you sure you want to delete the server ${this.dom.sanitize(SecurityContext.HTML, server.Name)}?`
			} as ConfirmDialogData
		});

		dialog.afterClosed().subscribe((result: boolean) => {
			if (result) {
				this.Loading$.next(true);

				this.servers.deleteServer(server)
					.subscribe(() => {
						let currentServers = this.Servers$.getValue();
						let index = currentServers.indexOf(server);

						if (index >= 0) {
							currentServers.splice(index, 1);
							this.Servers$.next(currentServers);
						}

						this.Loading$.next(false);
					});
			}
		});
	}

	/** Whether the number of selected elements matches the total number of rows. */
	isAllSelected() {
		const numSelected = this.Selection.selected.length;
		const numRows = this.ServersSource.data.length;
		return numSelected == numRows;
	}

	/** Selects all rows if they are not all selected; otherwise clear selection. */
	masterToggle() {
		this.isAllSelected() ?
			this.Selection.clear() :
			this.ServersSource.data.forEach(row => this.Selection.select(row));
	}

	async changeServerStatus(server: ServerShowcase) {
		if (server.ProcessStatus == ProcessStatus.Updating) return; // Do nothing when updating

		if (typeof server.LoadingStatus$ != 'undefined') return; // Server has already an action being done...

		switch (server.ProcessStatus)
		{
			case ProcessStatus.Stopped:
				server.LoadingStatus$ = this.servers.startServer(server);
				break;

			case ProcessStatus.Started:
				server.LoadingStatus$ = this.servers.stopServer(server);
				break;
		}

		server.LoadingStatus$
			.pipe(finalize(() => {
				delete server.LoadingStatus$;
			}))
			.subscribe(() => {}, (err) => {
				console.log(err);
				this.snack.open("There has been an error performing this action...", undefined, {
					duration: 5000
				});
			});
	}
}
