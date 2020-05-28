import { Component, OnInit, NgZone } from '@angular/core';
import { Location } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { FormGroup, FormControl, Validators, FormArray } from '@angular/forms';

import { Observable, of, Subject, BehaviorSubject, combineLatest } from 'rxjs';
import { catchError, map, tap, delay, skip, take, finalize, filter } from 'rxjs/operators';

import * as ipAddr from 'ipaddr.js';

import { Server, MonitorUser, Game, Group, ProcessPriorityClass, ProcessStatus } from '../../../definitions/interfaces';
import { ServerService } from '../../../services/server.service';
import { UserService } from '../../../services/user.service';
import { GameDialogComponent } from '../../dialogs/game-dialog/game-dialog.component';
import { EventService } from '../../../services/event.service';

interface CategoryOrganized {
	Category: string;
	Members: Game[];
}

@Component({
	selector: 'app-server-details',
	templateUrl: './server-details.component.html',
	styleUrls: ['./server-details.component.scss']
})
export class ServerDetailsComponent implements OnInit {

	constructor(
		private route: ActivatedRoute,
		private router: Router,
		private location: Location,
		private dialog: MatDialog,
		private snackBar: MatSnackBar,
		private servers: ServerService,
		private users: UserService,
		private events: EventService
	) { }

	// Editable data
	public Id?: number;
	public Server$ = new BehaviorSubject<Server>(new Server());

	public serverForm: FormGroup;

	// Required data
	public Users$ = new BehaviorSubject<MonitorUser[]>([]);
	public Games$ = new BehaviorSubject<Game[]>([]);
	public GamesCategorized$ = new Subject<CategoryOrganized[]>();
	public Groups$ = new BehaviorSubject<Group[]>([]);

	public processPriorityClass = ProcessPriorityClass;

	// Current state data
	public Loading$ = new Subject<boolean>();
	public AllowedUserEdit = false;
	public Errored = false;

	public hasError = (controlName: string, errorName: string) => {
		return this.serverForm.controls[controlName].hasError(errorName);
	}

	ngOnInit(): void {
		if (this.route.snapshot.paramMap.get('id') != null)
			this.Id = +this.route.snapshot.paramMap.get('id');

		this.serverForm = new FormGroup({
			Name: new FormControl('', [Validators.required]),
			Owner: new FormControl('', [Validators.required]),
			Group: new FormControl(),
			Enabled: new FormControl('', [Validators.required]),

			Executable: new FormControl(),
			Path: new FormControl(),
			Graceful: new FormControl('', [Validators.required]),
			RestartOnClose: new FormControl('', [Validators.required]),

			StartParams: new FormControl(),
			StartParamsHidden: new FormControl(),
			StartParamsModifyAllowed: new FormControl('', [Validators.required]),
			ProcessPriority: new FormControl('', [Validators.required]),
			EnvironmentVariables: new FormArray([], (arr: FormArray) => {
				let keys = [];
				for (let element of arr.controls) {
					let key = element.get('Key').value;
					if (keys.includes(key)) {
						return {
							duplicateKey: true
						};
					}
					keys.push(key);
				}
				return null;
			}),

			IP: new FormControl('', (form) => {
				if (form.value.trim() != '') {
					if (!ipAddr.isValid(form.value)) {
						return {
							invalidIP: true
						};
					}
				}
				return null;
			}),
			DisplayIP: new FormControl(),
			Port: new FormControl('', [Validators.required, (control: FormControl) => {
				let val: number = control.value;
				if (!isNaN(val)) {
					return (val >= 0 && val <= 65535) ? null : {
						numberLimit: true
					};
				}
				return null;
			}]),

			Game: new FormControl('', [Validators.required]),
			Branch: new FormControl(),
			BranchPassword: new FormControl()
		});

		this.Games$.subscribe((games) => {
			let categories = {};

			for (let game of games) {
				categories[game.Engine] = categories[game.Engine] ?? [];
				categories[game.Engine].push(game);
			}

			let categoryObjects = Object.keys(categories).map(i => ({ Category: i, Members: categories[i] }));

			this.GamesCategorized$.next(categoryObjects as any);
		});

		this.Loading$
		.pipe(delay(0))
		.subscribe(loading => this.events.emit("Loading", loading));

		this.Server$
		.subscribe(server => this.serverForm.patchValue(server));

		this.fetchDetails();
	}

	private fetchServer() {
		this.Loading$.next(true);

		combineLatest([
			this.servers.getServer(this.Id),
			this.Users$.pipe(skip(1)),
			this.Groups$.pipe(skip(1)),
			this.Games$.pipe(skip(1))
		])
		.pipe(
			take(1),
			finalize(() => {
				this.Loading$.next(false);
			})
		)
		.subscribe(([server, users, groups, games]) => {
			let serverEnvVars = server.EnvironmentVariables;
			delete server.EnvironmentVariables;

			for (let envVar of serverEnvVars) {
				let keyArray = {};
				for (let key of Object.keys(envVar)) {
					keyArray[key] = new FormControl(envVar[key], Validators.required);
				}

				this.getEnvVariables().push(new FormGroup(keyArray));
			}

			if (server.Game) server.Game = games.find(g => g.Id == server.Game.Id);
			if (server.Owner) server.Owner = users.find(u => u.Id == server.Owner.Id);
			if (server.Group) server.Group = groups.find(g => g.Id == server.Group.Id);

			this.Server$.next(server);
		}, (err) => {
			console.log(err);
			this.Errored = true;
		});
	}

	fetchDetails() {
		this.Errored = false;

		// Load server data

		if (this.Id) {
			this.serverForm.disable();
			this.fetchServer();
			this.Server$.pipe(skip(1), take(1)).subscribe(() => this.serverForm.enable());
		}

		// Load all required data
		this.servers.getGames()
			.pipe(
				catchError((err, caught) => {
					console.error(err);
					this.Errored = true;

					return of([]);
				})
			)
			.subscribe((games) => this.Games$.next(games));

		this.users.getUsers()
			.pipe(
				tap(
					res => this.AllowedUserEdit = true,
					err => {
						this.Errored = true;
						this.AllowedUserEdit = false;
					} //,
					// () => this.Loading$.next(false)
				),
				catchError((err, caught) => {
					console.error(err);

					return of([
						this.Server$.getValue().Owner
					]);
				})
			)
			.subscribe((users) => this.Users$.next(users));

		this.users.getGroups()
			.pipe(
				catchError((err, caught) => {
					console.error(err);

					return of([]);
				})
			)
			.subscribe((groups) => this.Groups$.next(groups));
	}

	update() {
		this.Loading$.next(true);

		let server = this.Server$.getValue();
		Object.assign(server, this.serverForm.value);

		if (!this.Id) {
			this.servers.addServer(server)
			.pipe(finalize(() => {
				this.Loading$.next(false);
			}))
			.subscribe((server) => {
				this.Id = server.Id;
				this.location.replaceState(`/main/servers/${this.Id}/details`);

				this.snackBar.open('Server created succesfully!', undefined, {
					duration: 5000
				});

				this.servers.getServerUpdated()
				.pipe(
					filter(s => s.Id == this.Id && s.ProcessStatus == ProcessStatus.Updating),
					take(1)
				)
				.subscribe(s => {
					var snack = this.snackBar.open('The server has just started installing!', 'Open update console', {
						duration: 5000
					});

					snack.onAction().subscribe(() => {
						this.router.navigate([`/main/servers/${this.Id}/update`]);
					});
				});
			}, (err) => {
				this.snackBar.open('There has been an error while creating the server...', undefined, {
					duration: 5000
				});
				console.error(err);
				this.Errored = true;
			});
		} else {
			this.servers.updateServer(server)
				.pipe(finalize(() => {
					this.Loading$.next(false);
				}))
				.subscribe(() => {
					this.snackBar.open('Server updated succesfully!', undefined, {
						duration: 5000
					});
				}, (err) => {
					this.snackBar.open('There has been an error while updating the server...', undefined, {
						duration: 5000
					});
					console.error(err);
					this.Errored = true;
				});
		}
	}

	createGame() { // TODO: Implement SteamID shit with server
		let dialog = this.dialog.open(GameDialogComponent, {
			// height: '600px',
			// width: '400px',
			data: new Game()
		});

		dialog.afterClosed().subscribe(result => {
			this.servers.addGame(result)
			.subscribe(game => {
				// this.Games$ = of(this.lastGames);
				let currentGames = this.Games$.getValue();
				currentGames.push(game);
				this.Games$.next(currentGames);
				// of(currentGames).subscribe(this.Games$);
				// this.Server.Game = game;
				this.serverForm.get('Game').setValue(game);
			}, err => {
				console.log("Could not create game");
			});
		});
	}

	getEnvVariables() {
		return this.serverForm.get('EnvironmentVariables') as FormArray;
	}

	addEnvVariable() {
		const newControl = new FormGroup({
			Key: new FormControl('', [Validators.required]),
			Value: new FormControl('', [Validators.required])
		});

		// newControl.markAsPristine();
		// newControl.markAsUntouched();
		// newControl.stine();
		// newControl.get('Value').markAsPget('Key').markAsPriristine();

		this.getEnvVariables().push(newControl);
		newControl.markAsUntouched();
	}

	removeEnvVariable(envVar) {
		this.getEnvVariables().removeAt(envVar);
	}

}
