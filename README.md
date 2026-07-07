# PeerAssist

PeerAssist is a peer-to-peer learning and collaboration platform that connects people who need help with others who have the right skills and are available to assist. The idea behind this project is to make learning more accessible by allowing users to receive guidance from peers who have similar experience and can explain concepts in a simple and relatable way.

The system also encourages users to help others by rewarding active contributors through ratings and a helper ranking system. Recruiters can view top-performing helpers based on their contributions and performance within the platform.

This project was developed as our final year project using ASP.NET Core MVC and SQL Server.

## Features

- User registration with email verification
- Secure login and authentication
- Create and manage user profiles
- Skill-based helper matching
- Intelligent helper ranking using a Weighted Sum Model
- Real-time chat between helpers and help seekers
- Request management
- Rating and feedback system
- Helper performance tracking
- Admin dashboard for managing users and approvals
- Recruiter view for identifying active and highly rated helpers

## Technologies Used

- ASP.NET Core MVC (.NET 8)
- C#
- SQL Server
- Dapper
- SignalR
- Bootstrap 5
- JavaScript
- jQuery
- HTML
- CSS

## Matching Algorithm

One of the main features of PeerAssist is the helper recommendation system.

Instead of randomly suggesting helpers, the system filters available users based on skills, approval status, availability, and schedule. Eligible helpers are then ranked using a Weighted Sum Model that considers:

- Responsiveness
- User ratings
- Experience level

This helps recommend the most suitable helper for each request.

## Project Structure

```
PeerAssist
│
├── PeerAssist.UI
├── PeerAssist.Data
├── wwwroot
├── Controllers
├── Models
├── Views
├── Services
├── Stored Procedures
├── appsettings.json
└── PeerAssist.sln
```

## Getting Started

### Requirements

- Visual Studio 2022
- .NET 8 SDK
- SQL Server
- SQL Server Management Studio (SSMS)

### Installation

1. Clone the repository.

```bash
git clone https://github.com/yourusername/PeerAssist.git
```

2. Open the solution in Visual Studio.

3. Create a SQL Server database.

4. Run the SQL scripts included in the project to create the required tables and stored procedures.

5. Update the connection string inside **appsettings.json**.

6. Restore NuGet packages.

7. Run the project.

The application will open in your browser after the project starts.

## Screenshots



- Home Page
<img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/978525ae-5b1f-43be-96be-7b143c7879cb" />


- 
- Login Page

- <img width="1916" height="992" alt="image" src="https://github.com/user-attachments/assets/9d9ed471-e59a-4852-a04d-c4a4f4bcb668" />

- Registration Page

- <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/700886d0-446d-4120-b86f-e17d76fd5c5b" />

- Dashboard

- <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/ee5f004a-f4e1-43e8-9dd3-237d21347ec9" />

- Help Request

- <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/cb818320-254d-4a25-8b72-2291d56bbd05" />

- Helper Recommendation
- <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/fe974c75-250e-458b-a037-ebf026069ff9" />

-

- Real-time Chat
-  <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/5664c0aa-7c73-418c-a950-29ddf3b98489" />
- Admin Dashboard
- <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/f8013060-4fba-40d2-b438-7b113240cfe7" />
<img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/a4eaa680-ff97-40ad-b135-e56b91dfb52c" />



## Future Improvements

Some features that can be added in the future include:

- Video and screen sharing
- Mobile application
- Push notifications
- Multi-language support
- AI-assisted helper recommendations
- Calendar integration
- Advanced analytics for recruiters

## What I Learned

Working on PeerAssist helped me improve my understanding of full-stack web development using ASP.NET Core MVC. During this project I gained practical experience with authentication, SQL Server, SignalR, Dapper, real-time communication, database design, and implementing an algorithm to recommend suitable helpers based on multiple factors.

## Authors

- Bidushi Gautam

