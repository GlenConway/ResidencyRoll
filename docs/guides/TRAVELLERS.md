# Travellers on one account

One login can track more than one person. A spouse or a child who never signs in still has their own trips, day counts, and home country. The signed-in person is a traveller too: the one linked to the account.

## In the app

The header has a **Traveller** list. It starts on you, and the last choice stays in this browser (`residencyroll_traveller_id`). Nothing on the server remembers which person a screen is showing, so two browsers can look at two people at once.

Home, Manage Trips, and Forecast show that person's name beside the page title and load that person's trips and days. The home country control on those pages saves to the person you are looking at.

**Travellers** (in the side navigation) adds a person, renames them, and sets a home country. You cannot delete yourself. Deleting someone who has trips asks you to confirm that those trips go too.

On **Add Trip**, when the account has more than one person, checkboxes choose who receives the leg. The person in the header starts checked. One save writes the same leg for every checked person.

CSV export of trips adds a **Traveller** column at the end. A file from an older version, without that column, still imports, and every row goes to the person selected in the header (or to you, when nobody is selected). A file that has the column sends each row to the named person, and creates an error for a name that is not on the account. The daily presence CSV also ends with a traveller column.

## Before you deploy

Copy the SQLite database before upgrading a database that already has trips. Locally that file is under `./data`. In Docker it is `/app/data`. The upgrade adds a traveller for each existing account, names that person "Me" until the next sign-in, copies the profile home country onto them, and attaches every existing trip. A second start does not change those rows. Sign-in replaces "Me" with the name from the login when that name is still "Me" and no other traveller already uses it.

A database backup from System Info is a copy of the SQLite file, so it already contains the traveller column after the upgrade.

## From an AI client

Ask about another person by name. The client should call `list_travellers` and then pass `traveller` on the trip and residency tools. See [Connect an AI client](MCP_SETUP.md).
