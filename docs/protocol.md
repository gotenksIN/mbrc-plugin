# MusicBee Remote Protocol Documentation

This document describes the communication protocol between MusicBee Remote clients and the plugin.

## Table of Contents

- [Overview](#overview)
- [Connection Architecture](#connection-architecture)
- [Message Format](#message-format)
- [Protocol Handshake](#protocol-handshake)
- [Quick Reference](#quick-reference)
- [Protocol Versions](#protocol-versions)
- [Commands by Category](#commands-by-category)
- [Broadcast Events](#broadcast-events)
- [Data Models](#data-models)
- [Best Practices](#best-practices)

---

## Overview

MusicBee Remote uses a TCP socket-based protocol with JSON messages. The protocol supports multiple versions, with newer versions adding features while maintaining backward compatibility.

| Property | Value |
|----------|-------|
| Transport | TCP Socket |
| Default Port | 3000 (configurable) |
| Message Format | JSON |
| Encoding | UTF-8 (no BOM) |
| Message Terminator | CRLF (`\r\n`) |
| Current Version | 4 |
| Supported Versions | 2, 2.1, 3, 4 |

**Important:** All messages must be encoded as UTF-8 without a Byte Order Mark (BOM). The plugin expects and sends UTF-8 encoded text.

---

## Connection Architecture

### Dual Socket Pattern

Clients are recommended to establish **two separate connections** to the plugin:

#### 1. Main Socket (Broadcast-enabled)
- Receives all broadcast events (track changes, player state, etc.)
- Used for real-time UI updates
- Default behavior when connecting

#### 2. Data Socket (No-broadcast)
- Does not receive broadcast events
- Used for heavy data requests (album covers, large lists)
- Prevents broadcast queue buildup during long operations

### Why Use Dual Sockets?

When fetching heavy data like album covers or paginated library requests:
- These operations can take significant time
- During this time, broadcasts continue to queue up on the main socket
- This can cause memory issues and delayed UI updates
- Using a separate no-broadcast socket isolates heavy requests

### Example Connection Pattern

```
┌─────────────────────────────────────────────────────────────┐
│                         Client                               │
├─────────────────────────────────────────────────────────────┤
│  Main Socket (port 3000)          Data Socket (port 3000)   │
│  ├─ Broadcasts: enabled           ├─ Broadcasts: disabled   │
│  ├─ Track changes                 ├─ Album cover requests   │
│  ├─ Player state updates          ├─ Library browsing       │
│  └─ Real-time events              └─ Paginated queries      │
└─────────────────────────────────────────────────────────────┘
```

---

## Message Format

### Request Format

```json
{"context": "command_name", "data": <payload>}\r\n
```

- `context`: Command identifier (string)
- `data`: Command payload (varies by command - can be object, string, number, or null)

### Response Format

```json
{"context": "command_name", "data": <response_payload>}\r\n
```

### Examples

**Simple command (no data):**
```json
{"context": "playernext", "data": null}
```

**Command with string data:**
```json
{"context": "playervolume", "data": 75}
```

**Command with object data:**
```json
{
  "context": "nowplayingqueue",
  "data": {
    "queue": "next",
    "data": ["file:///path/to/song.mp3"]
  }
}
```

---

## Protocol Handshake

Every client should perform a protocol handshake after connecting. This declares the client's protocol version and capabilities.

### Context
`protocol`

### Request Formats

**Legacy format (V2/V2.1):**
```json
{
  "context": "protocol",
  "data": 2
}
```

**Extended format (V3+):**
```json
{
  "context": "protocol",
  "data": {
    "protocol_version": 4,
    "no_broadcast": false,
    "client_id": "MyApp"
  }
}
```

### Handshake Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `protocol_version` | int | Client's protocol version (2, 3, or 4) |
| `no_broadcast` | bool | If `true`, this connection will not receive broadcasts |
| `client_id` | string | Optional client identifier for logging |

### Response

```json
{
  "context": "protocol",
  "data": 4
}
```

The server responds with its protocol version. The effective protocol is the minimum of client and server versions.

### Setting Up Dual Sockets

**Main socket handshake:**
```json
{
  "context": "protocol",
  "data": {
    "protocol_version": 4,
    "no_broadcast": false
  }
}
```

**Data socket handshake:**
```json
{
  "context": "protocol",
  "data": {
    "protocol_version": 4,
    "no_broadcast": true
  }
}
```

---

## Quick Reference

All available contexts with short descriptions, organized by category.

### System

| Context | Description | Since |
|---------|-------------|-------|
| `protocol` | Protocol version handshake (extended format with `no_broadcast` in V3+) | V2 |
| `player` | Player identification and client platform registration | V2 |
| `ping` | Keepalive ping request | V2.1 |
| `pong` | Keepalive pong response | V2.1 |
| `pluginversion` | Query the plugin version string | V2 |
| `init` | Request initial state sync (triggers multiple responses) | V2.1 |
| `verifyconnection` | Verify the connection is active | V4 |

### Player Control

| Context | Description | Since |
|---------|-------------|-------|
| `playerplay` | Start playback | V2.1 |
| `playerpause` | Pause playback | V2.1 |
| `playerplaypause` | Toggle play/pause state | V2 |
| `playerstop` | Stop playback | V2 |
| `playernext` | Skip to next track | V2 |
| `playerprevious` | Skip to previous track | V2 |
| `playervolume` | Get or set volume level (0-100) | V2 |
| `playermute` | Get, set, or toggle mute state | V2 |
| `playershuffle` | Get, set, or toggle shuffle mode | V2 |
| `playerrepeat` | Get, set, or toggle repeat mode (None/All/One) | V2 |
| `playerautodj` | Get, set, or toggle AutoDJ mode | V2 |
| `scrobbler` | Get, set, or toggle Last.fm scrobbling | V2 |
| `playerstatus` | Get full player status (state, volume, modes) | V2 |
| `playeroutput` | Get or set audio output device | V4 |
| `playeroutputswitch` | Switch to a specific output device | V4 |

### Now Playing Track

| Context | Description | Since |
|---------|-------------|-------|
| `nowplayingtrack` | Get current track info (artist, title, album, year) | V2 |
| `nowplayingdetails` | Get extended track metadata (genre, bitrate, etc.) | V4 |
| `nowplayingposition` | Get or set playback position in milliseconds | V2 |
| `nowplayingcover` | Get album artwork for current track | V2 |
| `nowplayinglyrics` | Get lyrics for current track | V2 |
| `nowplayingrating` | Get or set track rating | V2 |
| `nowplayinglfmrating` | Get or set Last.fm love/ban status | V2 |
| `nowplayingtagchange` | Modify track metadata tags | V4 |

### Now Playing List

| Context | Description | Since |
|---------|-------------|-------|
| `nowplayinglist` | Get the now playing queue (supports pagination) | V2 |
| `nowplayinglistplay` | Play a specific track from the queue by index | V2 |
| `nowplayinglistremove` | Remove a track from the queue by index | V2 |
| `nowplayinglistmove` | Move a track within the queue | V2 |
| `nowplayinglistsearch` | Search and play a track in the queue | V2 |
| `nowplayingqueue` | Queue files to the now playing list | V3 |
| `nowplayinglistchanged` | Broadcast: now playing list was modified | V2 |

### Library Search

| Context | Description | Since |
|---------|-------------|-------|
| `librarysearchtitle` | Search library by track title | V2 |
| `librarysearchartist` | Search library by artist name | V2 |
| `librarysearchalbum` | Search library by album name | V2 |
| `librarysearchgenre` | Search library by genre | V2 |

### Library Browse

| Context | Description | Since |
|---------|-------------|-------|
| `browsegenres` | Browse all genres (paginated) | V3 |
| `browseartists` | Browse all artists (paginated, supports album artists) | V3 |
| `browsealbums` | Browse all albums (paginated) | V3 |
| `browsetracks` | Browse all tracks (paginated) | V3 |

### Library Navigation

| Context | Description | Since |
|---------|-------------|-------|
| `libraryartistalbums` | Get all albums by a specific artist | V2 |
| `libraryalbumtracks` | Get all tracks on a specific album | V2 |
| `librarygenreartists` | Get all artists in a specific genre | V2 |

### Library Queue

| Context | Description | Since |
|---------|-------------|-------|
| `libraryqueuetrack` | Queue tracks matching a title | V2 |
| `libraryqueueartist` | Queue all tracks by an artist | V2 |
| `libraryqueuealbum` | Queue all tracks from an album | V2 |
| `libraryqueuegenre` | Queue all tracks in a genre | V2 |
| `libraryplayall` | Play entire library (optional shuffle) | V4 |

### Library Covers

| Context | Description | Since |
|---------|-------------|-------|
| `libraryalbumcover` | Get album cover by artist/album (supports pagination) | V4 |
| `librarycovercachebuildstatus` | Query cover cache build progress | V4 |

### Radio

| Context | Description | Since |
|---------|-------------|-------|
| `radiostations` | Get available radio stations (paginated) | V4 |

### Playlists

| Context | Description | Since |
|---------|-------------|-------|
| `playlistlist` | Get all playlists (supports pagination) | V2 |
| `playlistplay` | Play a specific playlist by URL | V3 |

### Error Responses

| Context | Description | Since |
|---------|-------------|-------|
| `error` | Error occurred processing a request | V2 |
| `notallowed` | Operation not permitted (authentication required) | V2 |

---

## Protocol Versions

### Version 2 (Base)

Initial protocol version with core functionality:
- Player controls (play, pause, stop, next, previous)
- Volume and mute control
- Shuffle and repeat modes
- Now playing track info
- Cover art and lyrics
- Ratings (track and Last.fm)
- Library search
- Queue operations
- Playlist listing

### Version 2.1

Additions:
- Ping/Pong keepalive mechanism
- Separate play and pause commands
- Init request for initial state sync

### Version 3

Additions:
- Pagination support for large lists
- Browse operations (genres, artists, albums, tracks)
- `no_broadcast` flag in handshake
- Enhanced now playing queue management
- Playlist play by URL

### Version 4

Additions:
- Output device switching
- Tag editing
- Album cover pagination and caching
- Radio stations
- Connection verification
- Extended track details
- Play all library with shuffle option

---

## Commands by Category

### System Commands

#### Protocol Handshake
| Property | Value |
|----------|-------|
| Context | `protocol` |
| Since | V2 (extended format V3+) |
| Broadcast | No |

**Request:**
```json
{
  "context": "protocol",
  "data": {
    "protocol_version": 4,
    "no_broadcast": false,
    "client_id": "MyApp"
  }
}
```

**Response:**
```json
{
  "context": "protocol",
  "data": 4
}
```

---

#### Player Identification
| Property | Value |
|----------|-------|
| Context | `player` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "player",
  "data": "Android"
}
```

**Response:**
```json
{
  "context": "player",
  "data": "MusicBee"
}
```

The data field identifies the client platform ("Android", "iOS", or other).

---

#### Ping/Pong Keepalive
| Property | Value |
|----------|-------|
| Context | `ping` / `pong` |
| Since | V2.1 |
| Broadcast | No |

**Ping:**
```json
{
  "context": "ping",
  "data": null
}
```

**Pong response:**
```json
{
  "context": "pong",
  "data": null
}
```

---

#### Plugin Version
| Property | Value |
|----------|-------|
| Context | `pluginversion` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "pluginversion",
  "data": null
}
```

**Response:**
```json
{
  "context": "pluginversion",
  "data": "1.5.0"
}
```

---

#### Connection Verification
| Property | Value |
|----------|-------|
| Context | `verifyconnection` |
| Since | V4 |
| Broadcast | No |

**Request:**
```json
{
  "context": "verifyconnection",
  "data": null
}
```

**Response:**
```json
{
  "context": "verifyconnection",
  "data": true
}
```

---

#### Initialize (Full State Sync)
| Property | Value |
|----------|-------|
| Context | `init` |
| Since | V2.1 |
| Broadcast | No (triggers multiple responses) |

Requests initial state. Server responds with multiple messages:
- `nowplayingtrack` - Current track info
- `nowplayingrating` - Track rating
- `nowplayinglfmrating` - Last.fm status
- `playerstatus` - Full player state
- `nowplayingcover` - Album art (broadcast to all)
- `nowplayinglyrics` - Lyrics (broadcast to all)

**Request:**
```json
{
  "context": "init",
  "data": null
}
```

---

### Player Control Commands

#### Play
| Property | Value |
|----------|-------|
| Context | `playerplay` |
| Since | V2.1 |
| Broadcast | Yes (`playerstate`) |

**Request:**
```json
{
  "context": "playerplay",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerplay",
  "data": true
}
```

---

#### Pause
| Property | Value |
|----------|-------|
| Context | `playerpause` |
| Since | V2.1 |
| Broadcast | Yes (`playerstate`) |

**Request:**
```json
{
  "context": "playerpause",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerpause",
  "data": true
}
```

---

#### Play/Pause Toggle
| Property | Value |
|----------|-------|
| Context | `playerplaypause` |
| Since | V2 |
| Broadcast | Yes (`playerstate`) |

**Request:**
```json
{
  "context": "playerplaypause",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerplaypause",
  "data": true
}
```

---

#### Stop
| Property | Value |
|----------|-------|
| Context | `playerstop` |
| Since | V2 |
| Broadcast | Yes (`playerstate`) |

**Request:**
```json
{
  "context": "playerstop",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerstop",
  "data": true
}
```

---

#### Next Track
| Property | Value |
|----------|-------|
| Context | `playernext` |
| Since | V2 |
| Broadcast | Yes (`nowplayingtrack`, etc.) |

**Request:**
```json
{
  "context": "playernext",
  "data": null
}
```

**Response:**
```json
{
  "context": "playernext",
  "data": true
}
```

---

#### Previous Track
| Property | Value |
|----------|-------|
| Context | `playerprevious` |
| Since | V2 |
| Broadcast | Yes (`nowplayingtrack`, etc.) |

**Request:**
```json
{
  "context": "playerprevious",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerprevious",
  "data": true
}
```

---

#### Volume
| Property | Value |
|----------|-------|
| Context | `playervolume` |
| Since | V2 |
| Broadcast | Yes |

**Set volume:**
```json
{
  "context": "playervolume",
  "data": 75
}
```

**Query volume:**
```json
{
  "context": "playervolume",
  "data": null
}
```

**Response:**
```json
{
  "context": "playervolume",
  "data": 75
}
```

---

#### Mute
| Property | Value |
|----------|-------|
| Context | `playermute` |
| Since | V2 |
| Broadcast | Yes |

**Set mute:**
```json
{
  "context": "playermute",
  "data": true
}
```

**Toggle mute:**
```json
{
  "context": "playermute",
  "data": "toggle"
}
```

**Query mute:**
```json
{
  "context": "playermute",
  "data": null
}
```

**Response:**
```json
{
  "context": "playermute",
  "data": true
}
```

---

#### Shuffle
| Property | Value |
|----------|-------|
| Context | `playershuffle` |
| Since | V2 (enhanced in V4) |
| Broadcast | Yes |

**Toggle shuffle:**
```json
{
  "context": "playershuffle",
  "data": "toggle"
}
```

**Set shuffle state (V4+):**
```json
{
  "context": "playershuffle",
  "data": "shuffle"
}
```

**Response (V2):**
```json
{
  "context": "playershuffle",
  "data": true
}
```

**Response (V3+):**
```json
{
  "context": "playershuffle",
  "data": "shuffle"
}
```

Shuffle states: `"off"`, `"shuffle"`, `"autodj"`

---

#### Repeat
| Property | Value |
|----------|-------|
| Context | `playerrepeat` |
| Since | V2 |
| Broadcast | Yes |

**Toggle repeat (cycles None → All → One → None):**
```json
{
  "context": "playerrepeat",
  "data": "toggle"
}
```

**Set repeat mode:**
```json
{
  "context": "playerrepeat",
  "data": "All"
}
```

**Response:**
```json
{
  "context": "playerrepeat",
  "data": "All"
}
```

Repeat modes: `"None"`, `"All"`, `"One"`

---

#### AutoDJ
| Property | Value |
|----------|-------|
| Context | `playerautodj` |
| Since | V2 |
| Broadcast | Yes |

**Toggle AutoDJ:**
```json
{
  "context": "playerautodj",
  "data": "toggle"
}
```

**Response:**
```json
{
  "context": "playerautodj",
  "data": true
}
```

---

#### Scrobbling
| Property | Value |
|----------|-------|
| Context | `scrobbler` |
| Since | V2 |
| Broadcast | Yes |

**Toggle scrobbling:**
```json
{
  "context": "scrobbler",
  "data": "toggle"
}
```

**Response:**
```json
{
  "context": "scrobbler",
  "data": true
}
```

---

#### Player Status (Full State)
| Property | Value |
|----------|-------|
| Context | `playerstatus` |
| Since | V2 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "playerstatus",
  "data": null
}
```

**Response:**
```json
{
  "context": "playerstatus",
  "data": {
    "playerstate": "Playing",
    "playervolume": "75",
    "playermute": false,
    "playershuffle": "off",
    "playerrepeat": "None",
    "scrobbler": true
  }
}
```

---

#### Output Device (V4+)
| Property | Value |
|----------|-------|
| Context | `playeroutput` |
| Since | V4 |
| Broadcast | Yes |

**Query devices:**
```json
{
  "context": "playeroutput",
  "data": null
}
```

**Set device:**
```json
{
  "context": "playeroutput",
  "data": "Speakers"
}
```

**Response:**
```json
{
  "context": "playeroutput",
  "data": {
    "active": "Speakers",
    "devices": ["Speakers", "Headphones", "HDMI Output"]
  }
}
```

---

#### Output Device Switch (V4+)
| Property | Value |
|----------|-------|
| Context | `playeroutputswitch` |
| Since | V4 |
| Broadcast | Yes |

Switches to a specific output device by name.

**Request:**
```json
{
  "context": "playeroutputswitch",
  "data": "Headphones"
}
```

**Response:**
```json
{
  "context": "playeroutput",
  "data": {
    "active": "Headphones",
    "devices": ["Speakers", "Headphones", "HDMI Output"]
  }
}
```

---

### Now Playing Commands

#### Track Info
| Property | Value |
|----------|-------|
| Context | `nowplayingtrack` |
| Since | V2 |
| Broadcast | Yes (on track change) |

**Request:**
```json
{
  "context": "nowplayingtrack",
  "data": null
}
```

**Response (V2):**
```json
{
  "context": "nowplayingtrack",
  "data": {
    "artist": "Artist Name",
    "title": "Song Title",
    "album": "Album Name",
    "year": "2024"
  }
}
```

**Response (V3+):**
```json
{
  "context": "nowplayingtrack",
  "data": {
    "artist": "Artist Name",
    "title": "Song Title",
    "album": "Album Name",
    "year": "2024",
    "path": "C:\\Music\\song.mp3"
  }
}
```

---

#### Track Details (V4+)
| Property | Value |
|----------|-------|
| Context | `nowplayingdetails` |
| Since | V4 |
| Broadcast | No |

**Request:**
```json
{
  "context": "nowplayingdetails",
  "data": null
}
```

**Response:**
```json
{
  "context": "nowplayingdetails",
  "data": {
    "albumArtist": "Album Artist",
    "genre": "Rock",
    "trackNo": "5",
    "trackCount": "12",
    "discNo": "1",
    "discCount": "1",
    "composer": "Composer Name",
    "publisher": "Record Label",
    "comment": "",
    "grouping": "",
    "encoder": "LAME",
    "kind": "mp3",
    "format": "MPEG-1 Layer 3",
    "size": "8542631",
    "channels": "2",
    "sampleRate": "44100",
    "bitrate": "320",
    "dateModified": "2024-01-15",
    "dateAdded": "2024-01-01",
    "lastPlayed": "2024-01-20",
    "playCount": "15",
    "skipCount": "2",
    "duration": "245000"
  }
}
```

---

#### Playback Position
| Property | Value |
|----------|-------|
| Context | `nowplayingposition` |
| Since | V2 |
| Broadcast | Yes (on seek) |

**Query position:**
```json
{
  "context": "nowplayingposition",
  "data": null
}
```

**Seek to position (milliseconds):**
```json
{
  "context": "nowplayingposition",
  "data": 120000
}
```

**Response:**
```json
{
  "context": "nowplayingposition",
  "data": {
    "current": 120000,
    "total": 245000
  }
}
```

---

#### Album Cover
| Property | Value |
|----------|-------|
| Context | `nowplayingcover` |
| Since | V2 |
| Broadcast | Yes (on track change) |

**Request:**
```json
{
  "context": "nowplayingcover",
  "data": null
}
```

**Response (V2):**
```json
{
  "context": "nowplayingcover",
  "data": "base64_encoded_image_data"
}
```

**Response (V3+):**
```json
{
  "context": "nowplayingcover",
  "data": {
    "status": 200,
    "cover": "base64_encoded_image_data"
  }
}
```

**Status codes:**
- `200` - Cover available and included
- `404` - Cover not found
- `1` - Cover ready (not included in this response)

---

#### Lyrics
| Property | Value |
|----------|-------|
| Context | `nowplayinglyrics` |
| Since | V2 |
| Broadcast | Yes (on track change) |

**Request:**
```json
{
  "context": "nowplayinglyrics",
  "data": null
}
```

**Response (V2):**
```json
{
  "context": "nowplayinglyrics",
  "data": "Lyrics text here..."
}
```

**Response (V3+):**
```json
{
  "context": "nowplayinglyrics",
  "data": {
    "status": 200,
    "lyrics": "Lyrics text here..."
  }
}
```

---

#### Track Rating
| Property | Value |
|----------|-------|
| Context | `nowplayingrating` |
| Since | V2 |
| Broadcast | Yes |

**Set rating:**
```json
{
  "context": "nowplayingrating",
  "data": "4"
}
```

**Clear rating:**
```json
{
  "context": "nowplayingrating",
  "data": ""
}
```

**Query rating:**
```json
{
  "context": "nowplayingrating",
  "data": null
}
```

**Response:**
```json
{
  "context": "nowplayingrating",
  "data": "4"
}
```

---

#### Last.fm Love/Ban
| Property | Value |
|----------|-------|
| Context | `nowplayinglfmrating` |
| Since | V2 |
| Broadcast | Yes |

**Toggle love:**
```json
{
  "context": "nowplayinglfmrating",
  "data": "toggle"
}
```

**Set love:**
```json
{
  "context": "nowplayinglfmrating",
  "data": "love"
}
```

**Set ban:**
```json
{
  "context": "nowplayinglfmrating",
  "data": "ban"
}
```

**Response:**
```json
{
  "context": "nowplayinglfmrating",
  "data": "Love"
}
```

Values: `"Normal"`, `"Love"`, `"Ban"`

---

#### Tag Change (V4+)
| Property | Value |
|----------|-------|
| Context | `nowplayingtagchange` |
| Since | V4 |
| Broadcast | No |

**Request:**
```json
{
  "context": "nowplayingtagchange",
  "data": {
    "tag": "artist",
    "value": "New Artist Name"
  }
}
```

**Response:**
```json
{
  "context": "nowplayingdetails",
  "data": {
    "albumArtist": "Album Artist",
    "genre": "Rock",
    "trackNo": "5",
    "trackCount": "12",
    "discNo": "1",
    "discCount": "1",
    "composer": "Composer Name",
    "publisher": "Record Label",
    "comment": "",
    "grouping": "",
    "encoder": "LAME",
    "kind": "mp3",
    "format": "MPEG-1 Layer 3",
    "size": "8542631",
    "channels": "2",
    "sampleRate": "44100",
    "bitrate": "320",
    "dateModified": "2024-01-15",
    "dateAdded": "2024-01-01",
    "lastPlayed": "2024-01-20",
    "playCount": "15",
    "skipCount": "2",
    "duration": "245000"
  }
}
```

Note: Returns updated track details after the tag change.

---

### Now Playing List Commands

#### Get List
| Property | Value |
|----------|-------|
| Context | `nowplayinglist` |
| Since | V2 (pagination in V3+) |
| Broadcast | No |

**Request (V2):**
```json
{
  "context": "nowplayinglist",
  "data": null
}
```

**Request (V3+ with pagination):**
```json
{
  "context": "nowplayinglist",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

**Response (V2):**
```json
{
  "context": "nowplayinglist",
  "data": [
    {"artist": "Artist", "title": "Song 1", "path": "C:\\Music\\song1.mp3", "position": 0},
    {"artist": "Artist", "title": "Song 2", "path": "C:\\Music\\song2.mp3", "position": 1}
  ]
}
```

**Response (V3+):**
```json
{
  "context": "nowplayinglist",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 250,
    "data": [
      {"artist": "Artist", "title": "Song 1", "path": "C:\\Music\\song1.mp3", "position": 0}
    ]
  }
}
```

---

#### Play Track from List
| Property | Value |
|----------|-------|
| Context | `nowplayinglistplay` |
| Since | V2 |
| Broadcast | Yes (`nowplayingtrack`, etc.) |

**Request:**
```json
{
  "context": "nowplayinglistplay",
  "data": 5
}
```

Note: Android clients use 1-based indexing (adjusted internally).

**Response:**
```json
{
  "context": "nowplayinglistplay",
  "data": true
}
```

---

#### Remove Track from List
| Property | Value |
|----------|-------|
| Context | `nowplayinglistremove` |
| Since | V2 |
| Broadcast | Yes (`nowplayinglistchanged`) |

**Request:**
```json
{
  "context": "nowplayinglistremove",
  "data": 5
}
```

**Response:**
```json
{
  "context": "nowplayinglistremove",
  "data": {
    "success": true,
    "index": 5
  }
}
```

---

#### Move Track in List
| Property | Value |
|----------|-------|
| Context | `nowplayinglistmove` |
| Since | V2 |
| Broadcast | Yes (`nowplayinglistchanged`) |

**Request:**
```json
{
  "context": "nowplayinglistmove",
  "data": {
    "from": 5,
    "to": 2
  }
}
```

**Response:**
```json
{
  "context": "nowplayinglistmove",
  "data": {
    "success": true,
    "from": 5,
    "to": 2
  }
}
```

---

#### Search Now Playing
| Property | Value |
|----------|-------|
| Context | `nowplayinglistsearch` |
| Since | V2 |
| Broadcast | Yes (if match found and played) |

**Request:**
```json
{
  "context": "nowplayinglistsearch",
  "data": "song title"
}
```

**Response:**
```json
{
  "context": "nowplayinglistsearch",
  "data": true
}
```

---

#### Queue Files (V3+)
| Property | Value |
|----------|-------|
| Context | `nowplayingqueue` |
| Since | V3 |
| Broadcast | Yes (`nowplayinglistchanged`) |

**Request:**
```json
{
  "context": "nowplayingqueue",
  "data": {
    "queue": "next",
    "play": null,
    "data": ["file:///path/to/song1.mp3", "file:///path/to/song2.mp3"]
  }
}
```

Queue types:
- `"playnow"` / `"now"` - Play immediately
- `"next"` - Queue as next track
- `"last"` - Queue at end
- `"addandplay"` / `"add-all"` - Add all and start playing

**Response:**
```json
{
  "context": "nowplayingqueue",
  "data": {
    "code": 200
  }
}
```

Response codes: `200` (success), `400` (invalid request), `500` (error)

---

### Library Commands

#### Search by Title
| Property | Value |
|----------|-------|
| Context | `librarysearchtitle` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "librarysearchtitle",
  "data": "search query"
}
```

**Response:**
```json
{
  "context": "librarysearchtitle",
  "data": [
    {
      "src": "C:\\Music\\song.mp3",
      "artist": "Artist",
      "title": "Song",
      "trackno": 1,
      "disc": 1,
      "album": "Album",
      "album_artist": "Album Artist",
      "genre": "Rock"
    }
  ]
}
```

---

#### Search by Artist
| Property | Value |
|----------|-------|
| Context | `librarysearchartist` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "librarysearchartist",
  "data": "artist name"
}
```

**Response:**
```json
{
  "context": "librarysearchartist",
  "data": [
    {
      "src": "C:\\Music\\song.mp3",
      "artist": "Artist Name",
      "title": "Song Title",
      "trackno": 1,
      "disc": 1,
      "album": "Album Name",
      "album_artist": "Album Artist",
      "genre": "Rock"
    }
  ]
}
```

---

#### Search by Album
| Property | Value |
|----------|-------|
| Context | `librarysearchalbum` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "librarysearchalbum",
  "data": "album name"
}
```

**Response:**
```json
{
  "context": "librarysearchalbum",
  "data": [
    {
      "src": "C:\\Music\\song.mp3",
      "artist": "Artist Name",
      "title": "Song Title",
      "trackno": 1,
      "disc": 1,
      "album": "Album Name",
      "album_artist": "Album Artist",
      "genre": "Rock"
    }
  ]
}
```

---

#### Search by Genre
| Property | Value |
|----------|-------|
| Context | `librarysearchgenre` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "librarysearchgenre",
  "data": "genre name"
}
```

**Response:**
```json
{
  "context": "librarysearchgenre",
  "data": [
    {
      "src": "C:\\Music\\song.mp3",
      "artist": "Artist Name",
      "title": "Song Title",
      "trackno": 1,
      "disc": 1,
      "album": "Album Name",
      "album_artist": "Album Artist",
      "genre": "Rock"
    }
  ]
}
```

---

#### Browse Genres (V3+)
| Property | Value |
|----------|-------|
| Context | `browsegenres` |
| Since | V3 |
| Broadcast | No |

**Request:**
```json
{
  "context": "browsegenres",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

**Response:**
```json
{
  "context": "browsegenres",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 45,
    "data": [
      {"genre": "Rock", "count": 150},
      {"genre": "Pop", "count": 85}
    ]
  }
}
```

---

#### Browse Artists (V3+)
| Property | Value |
|----------|-------|
| Context | `browseartists` |
| Since | V3 |
| Broadcast | No |

**Request:**
```json
{
  "context": "browseartists",
  "data": {
    "offset": 0,
    "limit": 100,
    "album_artists": false
  }
}
```

Set `album_artists: true` to browse album artists instead of track artists.

**Response:**
```json
{
  "context": "browseartists",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 250,
    "data": [
      {"artist": "Artist Name", "count": 25}
    ]
  }
}
```

---

#### Browse Albums (V3+)
| Property | Value |
|----------|-------|
| Context | `browsealbums` |
| Since | V3 |
| Broadcast | No |

**Request:**
```json
{
  "context": "browsealbums",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

**Response:**
```json
{
  "context": "browsealbums",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 500,
    "data": [
      {"album": "Album Name", "artist": "Artist Name", "count": 12}
    ]
  }
}
```

---

#### Browse Tracks (V3+)
| Property | Value |
|----------|-------|
| Context | `browsetracks` |
| Since | V3 |
| Broadcast | No |

**Request:**
```json
{
  "context": "browsetracks",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

**Response:**
```json
{
  "context": "browsetracks",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 5000,
    "data": [
      {
        "src": "C:\\Music\\song.mp3",
        "artist": "Artist Name",
        "title": "Song Title",
        "trackno": 1,
        "disc": 1,
        "album": "Album Name",
        "album_artist": "Album Artist",
        "genre": "Rock"
      }
    ]
  }
}
```

---

#### Get Artist Albums
| Property | Value |
|----------|-------|
| Context | `libraryartistalbums` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "libraryartistalbums",
  "data": "Artist Name"
}
```

**Response:**
```json
{
  "context": "libraryartistalbums",
  "data": [
    {"album": "Album 1", "artist": "Artist Name", "count": 12},
    {"album": "Album 2", "artist": "Artist Name", "count": 8}
  ]
}
```

---

#### Get Album Tracks
| Property | Value |
|----------|-------|
| Context | `libraryalbumtracks` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "libraryalbumtracks",
  "data": "Album Name"
}
```

**Response:**
```json
{
  "context": "libraryalbumtracks",
  "data": [
    {
      "src": "C:\\Music\\track1.mp3",
      "artist": "Artist Name",
      "title": "Track 1",
      "trackno": 1,
      "disc": 1,
      "album": "Album Name",
      "album_artist": "Album Artist",
      "genre": "Rock"
    },
    {
      "src": "C:\\Music\\track2.mp3",
      "artist": "Artist Name",
      "title": "Track 2",
      "trackno": 2,
      "disc": 1,
      "album": "Album Name",
      "album_artist": "Album Artist",
      "genre": "Rock"
    }
  ]
}
```

---

#### Get Genre Artists
| Property | Value |
|----------|-------|
| Context | `librarygenreartists` |
| Since | V2 |
| Broadcast | No |

**Request:**
```json
{
  "context": "librarygenreartists",
  "data": "Genre Name"
}
```

**Response:**
```json
{
  "context": "librarygenreartists",
  "data": [
    {"artist": "Artist 1", "count": 25},
    {"artist": "Artist 2", "count": 18}
  ]
}
```

---

#### Queue Track
| Property | Value |
|----------|-------|
| Context | `libraryqueuetrack` |
| Since | V2 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "libraryqueuetrack",
  "data": {
    "type": "next",
    "query": "Song Title"
  }
}
```

**Response:**
```json
{
  "context": "libraryqueuetrack",
  "data": true
}
```

---

#### Queue Artist
| Property | Value |
|----------|-------|
| Context | `libraryqueueartist` |
| Since | V2 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "libraryqueueartist",
  "data": {
    "type": "next",
    "query": "Artist Name"
  }
}
```

**Response:**
```json
{
  "context": "libraryqueueartist",
  "data": true
}
```

---

#### Queue Album
| Property | Value |
|----------|-------|
| Context | `libraryqueuealbum` |
| Since | V2 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "libraryqueuealbum",
  "data": {
    "type": "next",
    "query": "Album Name"
  }
}
```

**Response:**
```json
{
  "context": "libraryqueuealbum",
  "data": true
}
```

---

#### Queue Genre
| Property | Value |
|----------|-------|
| Context | `libraryqueuegenre` |
| Since | V2 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "libraryqueuegenre",
  "data": {
    "type": "next",
    "query": "Genre Name"
  }
}
```

**Response:**
```json
{
  "context": "libraryqueuegenre",
  "data": true
}
```

---

#### Play All Library (V4+)
| Property | Value |
|----------|-------|
| Context | `libraryplayall` |
| Since | V4 |
| Broadcast | Yes |

**Request (shuffle):**
```json
{
  "context": "libraryplayall",
  "data": true
}
```

**Request (in order):**
```json
{
  "context": "libraryplayall",
  "data": false
}
```

**Response:**
```json
{
  "context": "libraryplayall",
  "data": true
}
```

---

#### Album Cover (V4+)
| Property | Value |
|----------|-------|
| Context | `libraryalbumcover` |
| Since | V4 |
| Broadcast | No |

Use this on the **data socket** (no-broadcast) for heavy cover fetching.

**Single cover request:**
```json
{
  "context": "libraryalbumcover",
  "data": {
    "artist": "Artist Name",
    "album": "Album Name",
    "hash": "previous_hash_if_cached",
    "size": 300
  }
}
```

**Paginated cover request:**
```json
{
  "context": "libraryalbumcover",
  "data": {
    "offset": 0,
    "limit": 20
  }
}
```

**Response:**
```json
{
  "context": "libraryalbumcover",
  "data": {
    "status": 200,
    "artist": "Artist Name",
    "album": "Album Name",
    "cover": "base64_data",
    "hash": "sha1_hash"
  }
}
```

Status codes:
- `200` - Cover available
- `304` - Not modified (hash matches)
- `400` - Invalid request (empty album)
- `404` - Cover not found

---

#### Cover Cache Status (V4+)
| Property | Value |
|----------|-------|
| Context | `librarycovercachebuildstatus` |
| Since | V4 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "librarycovercachebuildstatus",
  "data": null
}
```

**Response:**
```json
{
  "context": "librarycovercachebuildstatus",
  "data": true
}
```

Returns `true` if cache is currently being built.

---

#### Radio Stations (V4+)
| Property | Value |
|----------|-------|
| Context | `radiostations` |
| Since | V4 |
| Broadcast | No |

**Request:**
```json
{
  "context": "radiostations",
  "data": {
    "offset": 0,
    "limit": 50
  }
}
```

**Response:**
```json
{
  "context": "radiostations",
  "data": {
    "offset": 0,
    "limit": 50,
    "total": 25,
    "data": [
      {"name": "Station Name", "url": "http://stream.url/radio"}
    ]
  }
}
```

---

### Playlist Commands

#### Get Playlists
| Property | Value |
|----------|-------|
| Context | `playlistlist` |
| Since | V2 |
| Broadcast | Yes (on change) |

**Request (V2):**
```json
{
  "context": "playlistlist",
  "data": null
}
```

**Request (V3+):**
```json
{
  "context": "playlistlist",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

**Response (V2):**
```json
{
  "context": "playlistlist",
  "data": [
    {"name": "Favorites", "url": "playlist://favorites"},
    {"name": "Rock Mix", "url": "playlist://rock-mix"}
  ]
}
```

**Response (V3+):**
```json
{
  "context": "playlistlist",
  "data": {
    "offset": 0,
    "limit": 100,
    "total": 15,
    "data": [
      {"name": "Favorites", "url": "playlist://favorites"},
      {"name": "Rock Mix", "url": "playlist://rock-mix"}
    ]
  }
}
```

---

#### Play Playlist (V3+)
| Property | Value |
|----------|-------|
| Context | `playlistplay` |
| Since | V3 |
| Broadcast | Yes |

**Request:**
```json
{
  "context": "playlistplay",
  "data": "playlist_url_or_path"
}
```

**Response:**
```json
{
  "context": "playlistplay",
  "data": true
}
```

---

## Broadcast Events

Broadcasts are automatically sent to all connected clients (unless `no_broadcast: true` was set during handshake).

### Player State Broadcasts

| Context | Trigger | Data |
|---------|---------|------|
| `playerstate` | Play/pause/stop | `"Playing"`, `"Paused"`, `"Stopped"` |
| `playervolume` | Volume change | Integer (0-100) |
| `playermute` | Mute toggle | Boolean |
| `playershuffle` | Shuffle change | Boolean (V2) or ShuffleState (V3+) |
| `playerrepeat` | Repeat change | `"None"`, `"All"`, `"One"` |
| `scrobbler` | Scrobbler toggle | Boolean |
| `playerautodj` | AutoDJ toggle | Boolean |
| `playeroutput` | Output device change | OutputDevice object |

### Track Broadcasts

| Context | Trigger | Data |
|---------|---------|------|
| `nowplayingtrack` | Track change | NowPlayingTrack/V2 object |
| `nowplayingposition` | Seek | Position object |
| `nowplayingcover` | Cover available | CoverPayload (V3+) or string (V2) |
| `nowplayinglyrics` | Lyrics available | LyricsPayload (V3+) or string (V2) |
| `nowplayingrating` | Rating change | String |
| `nowplayinglfmrating` | Last.fm status change | LastfmStatus |

### List Broadcasts

| Context | Trigger | Data |
|---------|---------|------|
| `nowplayinglistchanged` | List modified | Notification only |
| `playlistlist` | Playlists changed | Updated list |

### Example Broadcast Sequence (Track Change)

When a track changes, clients receive:

```json
{
  "context": "nowplayingtrack",
  "data": {
    "artist": "...",
    "title": "...",
    "album": "...",
    "year": "...",
    "path": "..."
  }
}
{
  "context": "nowplayingposition",
  "data": {
    "current": 0,
    "total": 245000
  }
}
{
  "context": "nowplayingcover",
  "data": {
    "status": 200,
    "cover": "base64..."
  }
}
{
  "context": "nowplayinglyrics",
  "data": {
    "status": 200,
    "lyrics": "..."
  }
}
{
  "context": "nowplayingrating",
  "data": "4"
}
{
  "context": "nowplayinglfmrating",
  "data": "Normal"
}
```

---

## Data Models

### Enumerations

#### PlayState
```
"Undefined" | "Stopped" | "Playing" | "Paused"
```

#### RepeatMode
```
"Undefined" | "None" | "All" | "One"
```

#### ShuffleState (V3+)
```
"off" | "shuffle" | "autodj"
```

#### LastfmStatus
```
"Normal" | "Love" | "Ban"
```

### Objects

#### PlayerStatus
```json
{
  "playerstate": "Playing",
  "playervolume": "75",
  "playermute": false,
  "playershuffle": "off",
  "playerrepeat": "None",
  "scrobbler": true
}
```

#### NowPlayingTrack (V2)
```json
{
  "artist": "string",
  "title": "string",
  "album": "string",
  "year": "string"
}
```

#### NowPlayingTrackV2 (V3+)
```json
{
  "artist": "string",
  "title": "string",
  "album": "string",
  "year": "string",
  "path": "string"
}
```

#### CoverPayload
```json
{
  "status": 200,
  "cover": "base64_string"
}
```

Note: `cover` field is omitted when null (status 1 or 404).

#### LyricsPayload
```json
{
  "status": 200,
  "lyrics": "string"
}
```

#### PlaybackPosition
```json
{
  "current": 120000,
  "total": 245000
}
```

#### Page (Paginated Response)
```json
{
  "offset": 0,
  "limit": 100,
  "total": 500,
  "data": [...]
}
```

#### Track
```json
{
  "src": "C:\\Music\\song.mp3",
  "artist": "string",
  "title": "string",
  "trackno": 1,
  "disc": 1,
  "album": "string",
  "album_artist": "string",
  "genre": "string"
}
```

#### NowPlayingListTrack
```json
{
  "artist": "string",
  "title": "string",
  "path": "string",
  "position": 0
}
```

#### GenreData
```json
{
  "genre": "string",
  "count": 0
}
```

#### ArtistData
```json
{
  "artist": "string",
  "count": 0
}
```

#### AlbumData
```json
{
  "album": "string",
  "artist": "string",
  "count": 0
}
```

#### Playlist
```json
{
  "url": "string",
  "name": "string"
}
```

#### RadioStation
```json
{
  "name": "string",
  "url": "string"
}
```

---

## Best Practices

### 1. Use Dual Sockets

```
Main Socket (broadcasts enabled):
- Real-time player state updates
- Track change notifications
- UI synchronization

Data Socket (no_broadcast: true):
- Album cover fetching
- Library browsing
- Large paginated requests
```

### 2. Handle Pagination

For large libraries, always use pagination:
```json
{
  "context": "browsetracks",
  "data": {
    "offset": 0,
    "limit": 100
  }
}
```

Fetch pages incrementally rather than requesting everything at once.

### 3. Cache Aggressively

- Use the `hash` field in cover requests to avoid re-downloading unchanged covers
- Status `304` indicates the cached version is still valid

### 4. Implement Keepalive

Send periodic ping messages to detect connection drops:
```json
{
  "context": "ping",
  "data": null
}
```

### 5. Handle Version Differences

Check the protocol version returned during handshake and adjust your requests accordingly:
- V2: Simple data types, no pagination
- V3: Object payloads, pagination support
- V4: Extended features (output devices, tag editing, etc.)

### 6. Process Broadcasts Efficiently

Broadcasts arrive frequently during playback. Implement debouncing for UI updates to avoid performance issues.

---

## Error Handling

### Error Response
```json
{
  "context": "error",
  "data": "Error message"
}
```

### Not Allowed Response
```json
{
  "context": "notallowed",
  "data": null
}
```

Sent when an operation is not permitted (e.g., unauthenticated client).

---

## Discovery

MusicBee Remote supports UDP multicast for service discovery on the local network.

- Multicast Address: `239.1.5.10`
- Port: `45345`

Clients can listen for discovery broadcasts to find available MusicBee instances without manual configuration.
