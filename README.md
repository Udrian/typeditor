# typeditor

## Options

Ordinary settings use the same registered property editors as the Properties
panel, selected by the declared `ISetting` value type (even for null values).
Edits pass typed values to the setting's save callback. Revert restores the
default value and refreshes the editor.

**Viewer > Viewer Clear Color** changes the editor scene's background immediately
and is saved in the Viewer settings context. Revert restores the original purple
background. Game scene backgrounds are not changed.

Shortcuts keep their key-combination capture field, conflict checks, and
Backspace/Delete clearing behavior.

Run the options editor regression tests with
`dotnet test TypeDitorTest\TypeDitorTest.csproj --filter FullyQualifiedName~SettingEditorTests`.

## Theme settings

The Theme dialog saves the mode and color scheme using `ThemeSettingContext`.
Choices are applied in increasing priority:

- **System**: the editor default, stored in the common application-data
  `TypeO/Settings/Theme.json` file.
- **Project**: the shared override in the project's `TypeO/Settings/Theme.json`.
- **User**: the current user's override for that project, stored in local
  application data under `TypeO/Settings/<ProjectName>/Theme.json`.

Select **Use inherited theme** in the Project or User scope to remove that
override. Removing a User override restores the Project theme, or System if
the project has no override. Higher-priority overrides still apply when editing
a lower-priority scope. Cancel discards unsaved theme choices.

Legacy flat theme files remain readable. The former editor-default file in local
application data is used until a System theme is saved. Saving writes the
setting-context format. System changes may require write permission to the
common application-data directory; save errors are displayed in the dialog.
Custom color-scheme files are unchanged.