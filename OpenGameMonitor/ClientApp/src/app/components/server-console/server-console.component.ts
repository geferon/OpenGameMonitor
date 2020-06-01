import { Component, OnInit, ChangeDetectorRef, Input, ViewChild } from '@angular/core';
import { Subject, from } from 'rxjs';
import { mergeMap, takeUntil, filter } from 'rxjs/operators';
import { ServerService } from '../../services/server.service';
import { Server } from '../../definitions/interfaces';
import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';

@Component({
	selector: 'server-console',
	templateUrl: './server-console.component.html',
	styleUrls: ['./server-console.component.scss']
})
export class ServerConsoleComponent implements OnInit {
	constructor(
		private servers: ServerService,
		private cdRef: ChangeDetectorRef
	) { }

	@ViewChild(CdkVirtualScrollViewport)
	public virtualScrollViewport?: CdkVirtualScrollViewport;

	@Input() server: Server | number;
	public Lines: string[] = [];

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		let id: number = typeof this.server == 'number' ? this.server : this.server.Id;

		from(this.servers.subscribeToServer(id))
			.pipe(
				mergeMap(() => this.servers.getServersConsoleMessages()),
				takeUntil(this.onDestroy),
				filter(([serverId, line]: [number, string]) => {
					return serverId == id;
				})
			)
			.subscribe(([serverId, line]: [number, string]) => {
				// this.Lines.push(line);
				this.Lines = [...this.Lines, line];
				// console.log(this.Lines);
				this.cdRef.detectChanges();

				if (this.virtualScrollViewport.measureScrollOffset('bottom') <= 20) {
					this.virtualScrollViewport.scrollTo({bottom: 0});
				}
			});
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		let subs = this.servers.unsubscribeFromServers().subscribe(() => subs.unsubscribe());
	}

}
