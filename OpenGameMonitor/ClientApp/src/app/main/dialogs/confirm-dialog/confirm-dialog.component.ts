import { Component, OnInit, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { GameDialogComponent } from '../game-dialog/game-dialog.component';

export interface ConfirmDialogData {
	Title: string;
	Text: string;
	ConfirmText?: string;
	CancelText?: string;
}

@Component({
	selector: 'app-confirm-dialog',
	templateUrl: './confirm-dialog.component.html',
	styleUrls: ['./confirm-dialog.component.scss']
})
export class ConfirmDialogComponent implements OnInit {

	constructor(
		public dialogRef: MatDialogRef<GameDialogComponent>,
		@Inject(MAT_DIALOG_DATA) public DialogData: ConfirmDialogData
	) { }

	ngOnInit(): void {
	}

	onCancel(): void {
		this.dialogRef.close(false);
	}

}
