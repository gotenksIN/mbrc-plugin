# MusicBee Remote API Debugger

A developer tool for testing, debugging, and comparing MusicBee Remote API responses across different plugin versions.

## Features

### Direct Connection Mode
Connect directly to a MusicBee Remote plugin instance to:
- Send raw JSON commands and view responses
- Test individual API endpoints
- Debug protocol issues

### Proxy Mode
Act as a man-in-the-middle proxy between a client and the plugin to:
- Intercept and record all request/response pairs
- Compare API behavior between different plugin versions
- Identify serialization differences and breaking changes

## Proxy Mode Usage

Proxy mode is designed for regression testing API compatibility between plugin versions.

### Setup

1. **Configure the proxy:**
   - Set the **Listen Port** (e.g., 3001) - where clients will connect
   - Set the **Target Host** and **Target Port** - the actual MusicBee Remote plugin (e.g., localhost:3000)

2. **Start the proxy:**
   - Click "Start Proxy" to begin intercepting traffic
   - The proxy will forward all messages between client and plugin while recording them

3. **Record a session:**
   - Connect your client (e.g., Android app) to the proxy port
   - Perform the actions you want to test
   - Click "Stop Recording" when done
   - Save the session with a descriptive name (e.g., "v1.4.1-baseline")

4. **Record another session:**
   - Switch to a different plugin version
   - Repeat steps 2-3 with a new session name (e.g., "v1.5.0-refactor")

### Comparing Sessions

1. **Load sessions:**
   - Use "Load Session" to load previously saved session files
   - Select two sessions in the "Session A" and "Session B" dropdowns

2. **Configure comparison:**
   - **Ignore Fields:** Comma-separated list of fields to ignore (e.g., `timestamp,id`)
   - **Ignore Array Order:** Check if array element order doesn't matter

3. **Compare:**
   - Click "Compare" to analyze differences
   - Results show matched request/response pairs with their comparison status

4. **Review differences:**
   - Click on any result row to open the diff viewer
   - Use the context filter dropdown to filter by specific API endpoints
   - Toggle between REQUEST and RESPONSE tabs
   - Navigate between differences using the arrow buttons

### Diff Viewer Features

- **Side-by-side comparison:** Session A (baseline) on left, Session B (current) on right
- **Line-level highlighting:** Added (green), removed (red), modified (yellow)
- **Character-level highlighting:** For modified lines, only the changed values are highlighted
- **Synchronized scrolling:** Both panels scroll together
- **Diff navigation:** Jump between differences with prev/next buttons
- **Copy buttons:** Copy formatted JSON from either side

## Session File Format

Sessions are saved as JSON files containing:
- Session metadata (name, timestamps)
- Array of request/response pairs with context information

## Common Use Cases

### Regression Testing
1. Record a baseline session with the stable version
2. Make code changes
3. Record a new session with the modified version
4. Compare to ensure no unintended changes

### Protocol Compatibility
1. Record sessions from different protocol versions
2. Compare to identify serialization differences
3. Verify backward compatibility

### Debugging
1. Record a session when an issue occurs
2. Analyze the request/response flow
3. Identify malformed messages or unexpected responses

## Keyboard Shortcuts

- In diff viewer, use arrow buttons or click to navigate differences
- Tab switching between REQUEST/RESPONSE is instant after initial load

## Notes

- Sessions are matched by their `context` field (API endpoint name)
- The comparison ignores message ordering - it matches by context
- Large responses are handled efficiently with background diff computation
