import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, BehaviorSubject, Subject, from, Subscription } from 'rxjs';
import { filter, takeUntil, mergeMap } from 'rxjs/operators';

import { ServerService } from '../../../services/server.service';
import { EventService } from '../../../services/event.service';
import { Server, MonitorUser, ProcessPriorityClass, ProcessStatus, ServerResourceMonitoringRegistry } from '../../../definitions/interfaces';
import { ChartOptions } from 'chart.js';
import { SingleDataSet } from 'ng2-charts';


let formatKilobytes = function(label: number, format: boolean = false) {
	if (label == 0) return '';

	var s = ['KB', 'MB', 'GB', 'TB', 'PB'];
	var e = Math.floor(Math.log(label) / Math.log(1024));
	var value = ((label / Math.pow(1024, Math.floor(e))).toFixed(2));
	e = (e < 0) ? (-e) : e;

	if (format) value += ' ' + s[e];
	return value;
};

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
	public Server$ = new Subject<Server>();
	public Registries$ = new BehaviorSubject<ServerResourceMonitoringRegistry[]>([]);

	public ActionLoading$: Subscription;
	public ProcessStatusEnum = ProcessStatus;

	// Config
	public playerChartOptions: ChartOptions = {
		responsive: true,
		maintainAspectRatio: false,
		scales: {
			yAxes: [{
				ticks: {
					min: 0,
					beginAtZero: true,
					stepSize: 1
				}
			}],
			xAxes: [{
				type: 'time',
				time: {
					unit: 'minute'
				}
			}]
		}
	};
	public memoryChartOptions: ChartOptions = {
		responsive: true,
		maintainAspectRatio: false,
		scales: {
			yAxes: [{
				ticks: {
					min: 0,
					beginAtZero: true,
					stepSize: 1024,
					callback: function (label, index, labels) {
						return formatKilobytes(label as number, true);
					}
				},
				scaleLabel: {
					display: true,

				}
			}],
			xAxes: [{
				type: 'time',
				time: {
					unit: 'minute'
				}
			}]
		}
	};
	public cpuChartOptions: ChartOptions = {
		responsive: true,
		maintainAspectRatio: false,
		scales: {
			yAxes: [{
				ticks: {
					min: 0,
					max: 100,
					beginAtZero: true,
					stepSize: 10
				}
			}],
			xAxes: [{
				type: 'time',
				time: {
					unit: 'minute'
				}
			}]
		}
	};
	public playerChartData: SingleDataSet = [];
	public memoryChartData: SingleDataSet = [];
	public cpuChartData: SingleDataSet = [];

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');

		this.servers.subscribeToServer(this.Id)
			.pipe(
				mergeMap(() => this.servers.getServersRecordsAdded()),
				takeUntil(this.onDestroy),
				filter(([serverId, record]: [number, object]) => {
					return serverId == this.Id;
				})
			)
			.subscribe(([serverId, record]: [number, ServerResourceMonitoringRegistry]) => {
				let regs = this.Registries$.getValue();
				regs.shift();
				record.TakenAt = new Date(record.TakenAt);
				regs.push(record);
				this.Registries$.next(regs);
			});

		this.Registries$.subscribe((regs) => {
			this.playerChartData = regs.map(r => ({
				x: r.TakenAt,
				y: r.ActivePlayers
			}));
			this.memoryChartData = regs.map(r => ({
				x: r.TakenAt,
				y: r.MemoryUsage
			}));
			this.cpuChartData = regs.map(r => ({
				x: r.TakenAt,
				y: r.CPUUsage
			}));
		});

		this.fetchDetails();
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		let subs = this.servers.unsubscribeFromServers().subscribe(() => subs.unsubscribe());
	}

	private fetchDetails() {
		this.servers.getServerRealtime(this.Id)
			.pipe(takeUntil(this.onDestroy))
			.subscribe(server => {
				this.Server$.next(server);
			});

		this.servers.getServersResourceMonitoringRegistries(this.Id)
			.subscribe(regs =>
				this.Registries$.next(
					regs
					.map(r => {
						r.TakenAt = new Date(r.TakenAt);
						return r;
					})
					.sort((a, b) => a.TakenAt.getTime() - b.TakenAt.getTime())
				)
			);
	}

	public startServer() {
		this.ActionLoading$ = this.servers.startServer(this.Id)
			.subscribe();
	}
	public restartServer() {
		this.ActionLoading$ = this.servers.stopServer(this.Id)
			.pipe(mergeMap(() => this.servers.startServer(this.Id)))
				.subscribe();
	}
	public stopServer() {
		this.ActionLoading$ = this.servers.stopServer(this.Id)
			.subscribe();
	}
	public updateServer() {
		this.ActionLoading$ = this.servers.startServerUpdate(this.Id)
			.subscribe();
	}

}
