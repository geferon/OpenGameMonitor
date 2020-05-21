import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { delay } from 'rxjs/operators';
import { Subject, Observable, BehaviorSubject } from 'rxjs';

import { EventService } from '../../services/event.service';
import { SettingsService } from '../../services/settings.service';
import { Setting } from '../../definitions/interfaces';

@Component({
	selector: 'app-settings',
	templateUrl: './settings.component.html',
	styleUrls: ['./settings.component.scss']
})
export class SettingsComponent implements OnInit {

	constructor(
		private events: EventService,
		private settings: SettingsService
	) { }

	public settingsForm: FormGroup;

	public Loading$ = new Subject<boolean>();
	public Settings$ = new BehaviorSubject<Setting[]>([]);

	public hasError = (controlName: string, errorName: string) => {
		return this.settingsForm.controls[controlName].hasError(errorName);
	}


	ngOnInit(): void {
		this.Loading$
			.pipe(delay(0))
			.subscribe(loading => this.events.emit("Loading", loading));

		this.settingsForm = new FormGroup({
			DefaultInstallDir: new FormControl('', [Validators.required]),
			// InstallSeparateGameDirs: new FormControl('', [Validators.required])
			DefaultServerDir: new FormControl('', [Validators.required])
		});

		this.fetchData();

		this.Settings$.subscribe(settings => {
			let settingsKeyValue: {[key: string]: any} = {};

			for (let setting of settings) {
				settingsKeyValue[setting.Key] = setting.Value;
			}

			this.settingsForm.patchValue(settingsKeyValue);
		});
	}

	fetchData(): void {
		this.settingsForm.disable();
		this.Loading$.next(true);

		this.settings.getSettings()
		.subscribe(settings => {
			this.Settings$.next(settings);
			this.Loading$.next(false);
			this.settingsForm.enable();
		});
	}

	update(): void {

	}

}
