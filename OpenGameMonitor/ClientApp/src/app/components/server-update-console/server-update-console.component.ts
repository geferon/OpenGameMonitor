import { Component, OnInit, ChangeDetectorRef, Input, ViewChild, ChangeDetectionStrategy } from '@angular/core';
import { Subject, from } from 'rxjs';
import { mergeMap, takeUntil, filter } from 'rxjs/operators';
import { ServerService } from '../../services/server.service';
import { Server } from '../../definitions/interfaces';
import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
// import { VirtualScrollerComponent } from 'ngx-virtual-scroller';

@Component({
	selector: 'server-update-console',
	templateUrl: './server-update-console.component.html',
	styleUrls: ['./server-update-console.component.scss'],
	changeDetection: ChangeDetectionStrategy.OnPush,
	// providers: [{provide: VIRTUAL_SCROLL_STRATEGY, useClass:  }]
})
export class ServerUpdateConsoleComponent implements OnInit {
	constructor(
		private servers: ServerService,
		private cdRef: ChangeDetectorRef
	) { }

	@ViewChild(CdkVirtualScrollViewport)
	public virtualScrollViewport?: CdkVirtualScrollViewport;
	// @ViewChild(VirtualScrollerComponent)
	// public virtualScrollComponent?: VirtualScrollerComponent;

	@Input() server: Server | number;
	public Lines: string[] = [];

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		let id: number = typeof this.server == 'number' ? this.server : this.server.Id;

		from(this.servers.subscribeToServer(id))
			.pipe(
				mergeMap(() => this.servers.getServersUpdateMessages()),
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
				// if (this.virtualScrollComponent.viewPortInfo.endIndex >= this.Lines.length - 1) {
				// 	this.virtualScrollComponent.scrollInto(line);
				// }
			});
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		let subs = this.servers.unsubscribeFromServers().subscribe(() => subs.unsubscribe());
	}

}
