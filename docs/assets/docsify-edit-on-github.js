/**
 * Minified by jsDelivr using Terser v5.19.2.
 * Original file: /npm/docsify-edit-on-github@1.0.3/index.js
 *
 * Do NOT use SRI with dynamically generated files! More information: https://www.jsdelivr.com/using-sri-with-dynamic-files
 */
!function(t){t.EditOnGithubPlugin={},t.EditOnGithubPlugin.create=function(n,i,e){function u(t){return header=['<div style="overflow: auto">','<p style="float: right"><a style="text-decoration: underline; cursor: pointer"','onclick="EditOnGithubPlugin.onClick(event)">',t,"</a></p>","</div>"].join("")}return e=e||"Edit on github",i=i||n.replace(/\/blob\//,"/edit/"),t.EditOnGithubPlugin.editDoc=function(t,n){var e=n.route.file;if(e){var u=i+e;return window.open(u),t.preventDefault(),!1}return!0},function(n,i){if(t.EditOnGithubPlugin.onClick=function(t){EditOnGithubPlugin.editDoc(t,i)},(r=e)&&"[object Function]"==={}.toString.call(r))n.afterEach((function(t){return u(e(i.route.file))+t}));else{var o=u(e);n.afterEach((function(t){return o+t}))}var r}}}(window);
//# sourceMappingURL=/sm/54cff3a4c90a14c5530ed6edd48405c4df7d5a42b7dcf9ed73a0fe89ed4d0105.map