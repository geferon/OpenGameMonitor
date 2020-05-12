import { Component, OnInit, NgZone } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Server, MonitorUser, Game, Group, ProcessPriorityClass } from '../../definitions/interfaces';
import { ServerService } from '../../services/server.service';
import { UserService } from '../../services/user.service';
import { Observable, of, Subject, BehaviorSubject } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { GameDialogComponent } from '../dialogs/game-dialog/game-dialog.component';
import { FormGroup, FormControl, Validators, FormArray } from '@angular/forms';

import * as ipAddr from 'ipaddr.js';
import { MatSnackBar } from '@angular/material/snack-bar';

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
		private servers: ServerService,
		private users: UserService,
		private dialog: MatDialog,
		private snackBar: MatSnackBar
	) { }

	// Editable data
	public Id?: number;
	public Server: Server;

	public serverForm: FormGroup;

	// Required data
	public Users$: Observable<MonitorUser[]>;
	// public Games$: Observable<Game[]>;
	public Games$: BehaviorSubject<Game[]> = new BehaviorSubject<Game[]>([]);
	public GamesCategorized$: Subject<CategoryOrganized[]> = new Subject<CategoryOrganized[]>();
	// private lastGames: Game[];
	public Groups$: Observable<Group[]>;

	public processPriorityClass = ProcessPriorityClass;

	// Current state data
	public Updating = false;
	public AllowedUserEdit = false;
	public Errored = false;

	public hasError = (controlName: string, errorName: string) => {
		return this.serverForm.controls[controlName].hasError(errorName);
	}

	ngOnInit(): void {
		this.Id = parseInt(this.route.snapshot.paramMap.get('id'));

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
			Port: new FormControl('', [Validators.required]),

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

		if (!this.Id) {
			this.Server = new Server();
			this.serverForm.patchValue(this.Server);
		}
		this.fetchDetails();
	}

	fetchServer() {
		this.servers.getServer(this.Id)
		.subscribe((server) => {
			let serverEnvVars = server.EnvironmentVariables;
			delete server.EnvironmentVariables;

			this.Server = server;
			this.serverForm.patchValue(this.Server);

			// TODO: Test
			for (let envVar of serverEnvVars) {
				let keyArray = {};
				for (let key of Object.keys(envVar)) {
					keyArray[key] = new FormControl(envVar[key], Validators.required);
				}

				this.getEnvVariables().push(new FormGroup(keyArray));
			}
		}, (err) => {
			this.Errored = true;
		}, () => {
			// Loading done?
		});
	}

	fetchDetails() {
		this.Errored = false;

		if (this.Id) {
			this.fetchServer();
		}

		this.servers.getGames()
			.pipe(
				catchError((err, caught) => {
					console.error(err);
					this.Errored = true;

					return of([]);
				})
			)
			.subscribe((games) => this.Games$.next(games));

		this.Users$ =
			this.users.getUsers()
				.pipe(
					tap(
						res => this.AllowedUserEdit = true,
						err => {
							this.Errored = true;
							this.AllowedUserEdit = false;
						}
					),
					catchError((err, caught) => {
						console.error(err);

						return of([
							this.Server.Owner
						]);
					})
				);

		this.Groups$ =
			this.users.getGroups()
				.pipe(
					catchError((err, caught) => {
						console.error(err);

						return of([]);
					})
				);
	}

	update() {
		this.Updating = true;

		Object.assign(this.Server, this.serverForm.value);

		if (!this.Id) {
			this.servers.addServer(this.Server)
			.subscribe((server) => {
				this.Server = server;
				this.Id = server.Id;

				this.snackBar.open('Server created succesfully!', undefined, {
					duration: 5000
				});
			}, (err) => {
				this.snackBar.open('There has been an error while creating the server...', undefined, {
					duration: 5000
				});
				console.error(err);
				this.Errored = true;
			}, () => {
				this.Updating = false;
			});
		} else {
			this.servers.updateServer(this.Server)
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
				}, () => {
					this.Updating = false;
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
