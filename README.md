# typeditor

## UI ownership

TypeDitor builds with TypeD as its only project reference. It owns the generic
component browser/explorer, Properties, Command History, Output, camera toolbar,
component/icon/content pickers, rename/move-target dialogs, and primitive
property templates. These use TypeD contracts rather than engine types.

TypeDCore owns entity/scene/drawable creation, custom-property and content-reference
dialogs, the engine Viewer and Content panels, and their view models. Color,
Vec2, and engine-content templates are resources of TypeDCore, not TypeDitor.
Module initialization attaches the engine panels; unloading removes them.
Existing panel IDs are preserved.

TypeDitor's generic dialog service raises TypeD's `CreateComponentDialogHook`
and `AddCustomPropertyDialogHook`. A loaded core module supplies those dialogs
and their asynchronous results. Without a provider, the request returns no
result. Neither project requires a reference to the other.

Build the standalone editor solution with `dotnet build TypeDitor.slnx`.

## Project window

The Project Window scans configured folders recursively for TypeO executable
projects and lists those with a matching `.typeo` project file. Configure the
**Project scan folders** list in Options with the folder picker; recently opened
projects appear first. The default scan folder is `Documents\TypeD`. The same
window is available at startup and from **File > Open Project**.
Each project shows the TypeD project format version stored in its `.typeo`
file. **Upgrade on open** means the file uses an older format and will be
upgraded (with a backup) when opened; **Needs newer TypeD** means the file is
from a newer editor. Module format upgrades are checked when the project opens.
When creating a project, pick its **Project Folder** from the configured scan
folders, or use **...** to choose another folder. That folder is added to the
scan folders once the project has been created.
Right-click a project for **Open**, **Rename**, **Delete**, and **Update
Modules**. Rename shows the project-related files and settings it found so you
can choose which associated paths to change. Delete confirms before removing
the project folder, its per-user settings/theme data, and its recent entry.
Update Modules opens the Modules dialog directly without opening or building
the project. Install, update, remove, and local-module actions save dependency
configuration immediately; modules are resolved, built, and loaded the next
time the project is opened. An already open project keeps using live module
management.

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

Selection, clipboard, and viewer-camera services now come from TypeD, not
TypeDCore. Clipboard command data uses TypeD's engine-independent world
coordinates; the core module still handles positioning engine nodes.

Run node clipboard snapshot checks with
`dotnet test ../typed/TypeDTest/TypeDTest.csproj --filter FullyQualifiedName~NodeClipboardModel`.
Run engine paste-position checks with
`dotnet test ../typeocore/TypeOCoreTest/TypeOCoreTest.csproj --filter FullyQualifiedName~ClipboardPosition`.

## Options

Settings use the same registered property editors as the Properties panel.
An explicitly associated `PropertyTemplateType` is used first; otherwise the
editor is selected by the setting or property's value type (even for null
values). This allows a registered editor to be reused for different value
types without changing the default editor for every value of those types.
Edits pass typed values to the setting's save callback. Revert restores the
inherited default value and refreshes the editor.

Use the filter at the top to search setting names, descriptions, categories,
command IDs, or shortcut bindings. Category selection narrows the search.

Right-click a setting or shortcut and choose **Set as project default** to save
only that setting (or individual command binding) in the project's
`TypeO/Settings/<Context>.json`. This is saved immediately and can be shared with
the project. User settings still take precedence. **Revert** clears the user's
override and restores the project default, falling back to the system or
built-in default when no project default exists.

Settings with a project default show a **Project default** label and a
**Remove project default** context-menu action. Removing one preserves user
overrides and other project settings; an inherited value updates immediately.
Project-default actions require an open project. Existing setting files remain
readable; newly edited scopes track explicit setting names so even a value
equal to the built-in default can override a project value.

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