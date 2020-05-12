import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../material.module';
import { GameDialogComponent } from './game-dialog/game-dialog.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';



@NgModule({
	imports: [
		CommonModule,
		MaterialModule,
		FormsModule,
		ReactiveFormsModule
	],
	declarations: [
		GameDialogComponent,
		ConfirmDialogComponent
	],
	exports: [
		GameDialogComponent,
		ConfirmDialogComponent
	]
})
export class DialogsModule { }
