// Minimal service worker: makes the ERP installable as a PWA without caching
// dynamic, authenticated server-rendered content. Navigation requests pass
// straight through to the network; nothing else is intercepted.
self.addEventListener('install', function (event) {
	self.skipWaiting();
});

self.addEventListener('activate', function (event) {
	event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', function (event) {
	if (event.request.mode === 'navigate') {
		event.respondWith(fetch(event.request));
	}
});
