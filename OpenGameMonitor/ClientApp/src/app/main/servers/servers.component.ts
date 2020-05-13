import { Component, OnInit, ViewChild, OnDestroy, SecurityContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { DomSanitizer } from '@angular/platform-browser';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { SelectionModel } from '@angular/cdk/collections';

import { catchError, map, takeUntil, delay } from 'rxjs/operators';
import { Observable, of, BehaviorSubject, pipe, Subject } from 'rxjs';

import { Server, Game } from '../../definitions/interfaces';
import { ServerService } from '../../services/server.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../dialogs/confirm-dialog/confirm-dialog.component';
import { EventService } from '../../services/event.service';

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
		private events: EventService
	) { }

	@ViewChild(MatPaginator, { static: true }) paginator: MatPaginator;

	public Loading$ = new Subject<boolean>();
	public Errored = false;

	// public Servers: Server[] = [];
	public Servers$: BehaviorSubject<Server[]> = new BehaviorSubject<Server[]>([]);
	public ServersSource = new MatTableDataSource<Server>([]);

	public ColumnsToDisplay = [
		"Select",
		"Name",
		"Owner",
		"Game",
		"IP",
		"Actions"
	];

	private readonly onDestroy = new Subject();

	public Selection: SelectionModel<Server>;

	ngOnInit(): void {
		this.ServersSource.paginator = this.paginator;

		const initialSelection = [];
		const allowMultiSelect = true;
		this.Selection = new SelectionModel<Server>(allowMultiSelect, initialSelection);

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

					return of([]);
				})
			)
			.subscribe(servers => {
				this.Servers$.next(servers);
				this.Loading$.next(false);
			});
	}

	deleteServer(server: Server) {
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

					if (index > 0) {
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
}
