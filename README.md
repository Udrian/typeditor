# typeditor

## Copying nodes

With the Component Explorer or Viewer focused:

- **Ctrl+D** duplicates the selected node as a sibling immediately after it.
- **Ctrl+C** copies the node, its properties, and its complete child hierarchy
  to the system clipboard as a snapshot.
- **Ctrl+V** pastes a new instance under the selected node in Component Explorer.
  In the Viewer it pastes under the hovered node, or the opened component root
  when the pointer is over empty space. In the Viewer,
  a writable `Vec2 Position` is placed at the current mouse world position,
  relative to the parent. Other nodes retain their copied position.

Duplicate and Paste support undo/redo. Each inserted root gets a fresh ID;
inherited child identities and nested property overrides are preserved.
Text fields retain their normal editing shortcuts. These command shortcuts can
be customized in Options.
Copy, Paste, and Duplicate are also available in the Command Palette when their
target is valid. The palette captures the selection and viewer paste location
before its search field takes focus.

Right-click a node in the Viewer for Duplicate, Copy, and Paste. Right-click
empty viewer space for Paste; the menu also offers Open Component and, when a
component is open, Close. Paste is disabled when the system clipboard contains
no compatible node. Clipboard snapshots require the same component/module format
versions and available component types in the destination project; copy again
after a format upgrade.

Run node clipboard regression tests with
`dotnet test TypeDitorTest\TypeDitorTest.csproj --filter FullyQualifiedName~NodeClipboardTests`.

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