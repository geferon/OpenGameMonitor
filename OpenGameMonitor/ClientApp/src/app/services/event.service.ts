import { Injectable } from '@angular/core';
import { Subject, Subscription, Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';

interface LoadingEvent {
	type: "Loading";
	data: boolean;
}

export type GenericEventsTypes = LoadingEvent;

type EventType<T> =
	T extends "Loading" ? LoadingEvent :
	never;

// Automatic types
type UnpackKey<T, K extends keyof T> = T[K];
export type GenericEvents = UnpackKey<GenericEventsTypes, "type">;


interface EventData {
	type: GenericEvents;
	data: any;
}

@Injectable({
	providedIn: 'root'
})
export class EventService {

	constructor() { }

	private eventsSubject$ = new Subject<GenericEventsTypes>();

	public emit<T extends GenericEvents>(event: GenericEvents, data: UnpackKey<EventType<T>, "data">) {
		// console.log(`Event emitter! ${event} - ${data}`);
		// console.trace();

		this.eventsSubject$.next({
			type: event,
			data: data
		});
	}

	public on<T extends GenericEvents>(event: GenericEvents): Observable<UnpackKey<EventType<T>, "data">> {
		return this.eventsSubject$
			.pipe(
				filter(ev => ev.type == event),
				map(ev => (ev as EventType<T>).data)
			);
	}
}
