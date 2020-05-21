import { Pipe, PipeTransform, EventEmitter, OnDestroy, ɵisPromise, ɵisObservable, ChangeDetectorRef } from '@angular/core';
import { Observable, SubscriptionLike } from 'rxjs';
import { stringify } from 'querystring';

interface SubscriptionStrategy {
	createSubscription(async: Observable<any> | Promise<any>, updateLatestValue: any): SubscriptionLike
		| Promise<any>;
	dispose(subscription: SubscriptionLike | Promise<any>): void;
	onDestroy(subscription: SubscriptionLike | Promise<any>): void;
}

class ObservableStrategy implements SubscriptionStrategy {
	createSubscription(async: Observable<any>, updateLatestValue: any): SubscriptionLike {
		return async.subscribe({
			next: updateLatestValue,
			error: (e: any) => {
				throw e;
			}
		});
	}

	dispose(subscription: SubscriptionLike): void {
		subscription.unsubscribe();
	}

	onDestroy(subscription: SubscriptionLike): void {
		subscription.unsubscribe();
	}
}

class PromiseStrategy implements SubscriptionStrategy {
	createSubscription(async: Promise<any>, updateLatestValue: (v: any) => any): Promise<any> {
		return async.then(updateLatestValue, e => {
			throw e;
		});
	}

	dispose(subscription: Promise<any>): void { }

	onDestroy(subscription: Promise<any>): void { }
}

const _promiseStrategy = new PromiseStrategy();
const _observableStrategy = new ObservableStrategy();

class FilterPipeMemberState {
	element: any; // Used as a key
	markForCheck: () => void;

	constructor(element: any, checkFunc: () => void) {
		this.element = element;
		this.markForCheck = checkFunc;
	}

	lastValue: any;
	subscription: SubscriptionLike | Promise<any> | null = null;
	obj: Observable<any> | Promise<any> | EventEmitter<any> | null = null;
	strategy: SubscriptionStrategy = null;

	subscribe(obj: Observable<any> | Promise<any> | EventEmitter<any>): void {
		this.obj = obj;
		this.strategy = this.selectStrategy(obj);
		this.subscription = this.strategy.createSubscription(
			obj,
			(value: Object) => this.updateLatestValue(obj, value)
		);
	}

	selectStrategy(obj: Observable<any> | Promise<any> | EventEmitter<any>): any {
		if (ɵisPromise(obj)) {
			return _promiseStrategy;
		}

		if (ɵisObservable(obj)) {
			return _observableStrategy;
		}

		throw Error(`InvalidPipeArgument: '${obj}' for pipe 'FilterPipe'`);
	}

	dispose(): void {
		this.strategy.dispose(this.subscription!);
		this.lastValue = null;
		this.subscription = null;
		this.obj = null;
	}

	updateLatestValue(async: any, value: Object): void {
		if (async === this.obj) {
			this.lastValue = value;
			this.markForCheck();
		}
	}
}

@Pipe({
	name: 'filtercallback',
	pure: false
})
export class FilterPipe implements PipeTransform, OnDestroy {
	private _memberStates: FilterPipeMemberState[] = [];

	constructor(private _ref: ChangeDetectorRef) { }

	transform(items: any[], callback: (item: any) => boolean | Promise<boolean> | Observable<boolean>): unknown {
		if (!items || !callback) {
			return items;
		}

		// Delete removed/missing items
		let i = this._memberStates.length;
		while (i--) {
			if (!items.includes(this._memberStates[i].element)) {
				this._memberStates[i].dispose();
				this._memberStates.splice(i, 1);
			}
		}

		// filter items array, items which match and return true will be
		// kept, false will be filtered out
		return items.filter(item => {
			// let returnedValue = typeof callback === 'function' ? callback(item) : callback;
			let returnedValue = callback(item);

			if (ɵisPromise(returnedValue) || ɵisObservable(returnedValue)) {
				let member = this._memberStates.find(m => m.element == item);
				if (!member) {
					member = new FilterPipeMemberState(item, this._ref.markForCheck.bind(this._ref));
					member.subscribe(returnedValue);
					this._memberStates.push(member);
				} else {
					if (member.obj != returnedValue) {
						member.dispose();
						member.subscribe(returnedValue);
					}
				}

				return member.lastValue ?? false;
			}

			return returnedValue;
		});
	}

	ngOnDestroy(): void {
		for (let member of this._memberStates) {
			member.dispose();
		}
	}
}
