import { Component, OnInit, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Game } from '../../../definitions/interfaces';
import { FormGroup, FormControl, Validators } from '@angular/forms';

@Component({
	selector: 'app-game-dialog',
	templateUrl: './game-dialog.component.html',
	styleUrls: ['./game-dialog.component.scss']
})
export class GameDialogComponent implements OnInit {

	constructor(
		public dialogRef: MatDialogRef<GameDialogComponent>,
		@Inject(MAT_DIALOG_DATA) public Game: Game
	) { }

	public gameForm: FormGroup;

	ngOnInit(): void {
		this.gameForm = new FormGroup({
			ID: new FormControl('', [Validators.required]),
			SteamID: new FormControl(),
			Name: new FormControl('', [Validators.required]),
			Engine: new FormControl('', [Validators.required])
		});

		this.gameForm.patchValue(Game);
	}

	onCancel(): void {
		this.dialogRef.close();
	}

	onSubmit(): void {
		console.log("Submitted");
		Object.assign(this.Game, this.gameForm.value);
	}

	public hasError = (controlName: string, errorName: string) => {
		return this.gameForm.controls[controlName].hasError(errorName);
	}

}
