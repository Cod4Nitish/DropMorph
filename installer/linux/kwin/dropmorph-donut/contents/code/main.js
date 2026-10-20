// KWin script (Plasma 6) for the experimental Wayland gesture. Wayland gives DropMorph no cursor position and no say
// over where its window opens, so this moves the donut window (class "DropMorph", title "DropMorph Donut") to the cursor
// whenever it is mapped. Install it with kpackagetool6 and enable it; see installer/linux/INSTALL.txt.

function isDonut(w) {
    return String(w.resourceClass).toLowerCase() === "dropmorph" && w.caption === "DropMorph Donut";
}

function centreOnCursor(w) {
    var p = workspace.cursorPos;
    var g = w.frameGeometry;
    var x = Math.round(p.x - g.width / 2);
    var y = Math.round(p.y - g.height / 2);
    if (g.x !== x || g.y !== y) {
        w.frameGeometry = { x: x, y: y, width: g.width, height: g.height };
    }
}

workspace.windowAdded.connect(function (w) {
    if (!isDonut(w)) {
        return;
    }
    // DropMorph also positions the window itself (twice, to cope with DPI changes), so win the last few moves.
    var left = 4;
    centreOnCursor(w);
    w.frameGeometryChanged.connect(function () {
        if (left > 0) {
            left--;
            centreOnCursor(w);
        }
    });
});
